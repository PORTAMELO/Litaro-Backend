using System.Globalization;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record PreEnrollmentDto(
    int PreEnrollmentId,
    short YearId,
    int StudentId,
    string StudentCode,
    string Document,
    string StudentName,
    int GradeId,
    string GradeName,
    string Status,
    DateTime CreatedAt,
    DateTime? RespondedAt,
    string? ResponseNote);

public record GeneratePreEnrollmentsResult(
    short SourceYearId,
    int Created,
    int AlreadyExisting,
    int AlreadyEnrolled,
    int WithoutNextGrade);

public record RestoreLostResult(int Restored, int SimatReverts);

public class PreEnrollmentService(AppDbContext db, EnrollmentPeriodService periods, NotificationService notifications)
{
    private const string ActiveEnrollment = "ACTIVE";
    private const string RestoredNote = "Cupo restablecido al corregir el plazo de prematrícula.";

    public async Task<RestoreLostResult> RestoreLostAsync(short yearId, int userId)
    {
        var period = await periods.FindAsync(yearId);

        if (period is null || EnrollmentPeriodService.Today() > period.PreEnrollmentEnd)
            throw new InvalidOperationException("Amplíe primero la fecha final de prematrícula.");

        var lost = await db.PreEnrollments
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Where(p => p.YearId == yearId && p.Status == PreEnrollmentStatuses.Lost)
            .ToListAsync();

        if (lost.Count == 0)
            return new RestoreLostResult(0, 0);

        var studentIds = lost.Select(p => p.StudentId).ToList();
        var lostNotices = await db.Notifications
            .Where(n =>
                n.NotificationType.Code == NotificationCodes.SimatCupoLost &&
                n.YearId == yearId &&
                n.StudentId != null &&
                studentIds.Contains(n.StudentId.Value))
            .ToListAsync();

        var now = DateTime.UtcNow;
        var reverts = 0;

        foreach (var item in lost)
        {
            item.Status = PreEnrollmentStatuses.Reserved;
            item.ResponseNote = null;
            item.RespondedAt = null;

            var notices = lostNotices.Where(n => n.StudentId == item.StudentId).ToList();

            foreach (var notice in notices.Where(n => n.Status == NotificationStatuses.Pending))
            {
                notice.Status = NotificationStatuses.Done;
                notice.DoneAt = now;
                notice.DoneById = userId;
                notice.DoneNote = RestoredNote;
            }

            if (notices.Any(n => n.Status == NotificationStatuses.Done && n.DoneNote != RestoredNote))
            {
                await notifications.QueueAsync(
                    NotificationCodes.SimatCupoRestored,
                    new Dictionary<string, string>
                    {
                        ["estudiante"] = FullName(item.Student.User),
                        ["documento"] = item.Student.User.DocumentNumber,
                        ["anio"] = yearId.ToString(),
                    },
                    new NotificationTarget(StudentId: item.StudentId, YearId: yearId));
                reverts++;
            }
        }

        await db.SaveChangesAsync();
        return new RestoreLostResult(lost.Count, reverts);
    }

    public async Task<GeneratePreEnrollmentsResult> GenerateAsync(short yearId)
    {
        var period = await periods.FindAsync(yearId);
        if (period is null)
            throw new InvalidOperationException("Configure primero las fechas de matrícula de ese año.");

        var sourceYearId = await db.AcademicYears
            .Where(y => y.YearId < yearId)
            .OrderByDescending(y => y.YearId)
            .Select(y => (short?)y.YearId)
            .FirstOrDefaultAsync();

        if (sourceYearId is null)
            throw new InvalidOperationException(
                $"No hay un año académico anterior a {yearId} del cual tomar a los estudiantes antiguos.");

        var sourceYear = sourceYearId.Value;
        var grades = await db.Grades.AsNoTracking().OrderBy(g => g.OrderNum).ToListAsync();

        var current = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.YearId == sourceYear && e.Status == ActiveEnrollment)
            .Select(e => new { e.StudentId, e.Classroom.GradeId })
            .ToListAsync();

        var withPreEnrollment = (await db.PreEnrollments
                .Where(p => p.YearId == yearId)
                .Select(p => p.StudentId)
                .ToListAsync())
            .ToHashSet();

        var enrolledThisYear = (await db.Enrollments
                .Where(e => e.YearId == yearId)
                .Select(e => e.StudentId)
                .ToListAsync())
            .ToHashSet();

        var created = 0;
        var alreadyExisting = 0;
        var alreadyEnrolled = 0;
        var withoutNextGrade = 0;

        foreach (var item in current)
        {
            if (withPreEnrollment.Contains(item.StudentId))
            {
                alreadyExisting++;
                continue;
            }

            if (enrolledThisYear.Contains(item.StudentId))
            {
                alreadyEnrolled++;
                continue;
            }

            Grade? nextGrade = null;
            var currentGrade = grades.FirstOrDefault(g => g.GradeId == item.GradeId);
            if (currentGrade is not null)
            {
                var currentOrder = currentGrade.OrderNum;
                nextGrade = grades.FirstOrDefault(g => g.OrderNum > currentOrder);
            }

            if (nextGrade is null)
            {
                withoutNextGrade++;
                continue;
            }

            db.PreEnrollments.Add(new PreEnrollment
            {
                YearId = yearId,
                StudentId = item.StudentId,
                GradeId = nextGrade.GradeId
            });
            created++;
        }

        await db.SaveChangesAsync();
        return new GeneratePreEnrollmentsResult(sourceYear, created, alreadyExisting, alreadyEnrolled, withoutNextGrade);
    }

    public async Task<List<PreEnrollmentDto>> ListAsync(short yearId)
    {
        await ExpireOverdueAsync(yearId);

        return await db.PreEnrollments
            .AsNoTracking()
            .Where(p => p.YearId == yearId)
            .OrderBy(p => p.Student.User.LastName)
            .ThenBy(p => p.Student.User.FirstName)
            .Select(p => new PreEnrollmentDto(
                p.PreEnrollmentId,
                p.YearId,
                p.StudentId,
                p.Student.StudentCode,
                p.Student.User.DocumentNumber,
                p.Student.User.FirstName + " " + p.Student.User.LastName,
                p.GradeId,
                p.Grade.Name,
                p.Status,
                p.CreatedAt,
                p.RespondedAt,
                p.ResponseNote))
            .ToListAsync();
    }

    public async Task RespondAsync(int id, bool confirm, string? note)
    {
        var item = await db.PreEnrollments
            .Include(p => p.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(p => p.PreEnrollmentId == id);

        if (item is null)
            throw new InvalidOperationException("No existe la prematrícula.");

        await ExpireOverdueAsync(item.YearId);

        if (item.Status != PreEnrollmentStatuses.Reserved)
            throw new InvalidOperationException(item.Status == PreEnrollmentStatuses.Lost
                ? "El plazo de prematrícula terminó y el cupo ya se perdió."
                : "Esta prematrícula ya tiene una respuesta registrada.");

        var cleanNote = note?.Trim();
        if (cleanNote is { Length: > 300 })
            cleanNote = cleanNote[..300];

        item.Status = confirm ? PreEnrollmentStatuses.Confirmed : PreEnrollmentStatuses.Declined;
        item.RespondedAt = DateTime.UtcNow;
        item.ResponseNote = string.IsNullOrEmpty(cleanNote) ? null : cleanNote;

        if (!confirm)
        {
            await notifications.QueueAsync(
                NotificationCodes.SimatCupoDeclined,
                new Dictionary<string, string>
                {
                    ["estudiante"] = FullName(item.Student.User),
                    ["documento"] = item.Student.User.DocumentNumber,
                    ["anio"] = item.YearId.ToString(),
                },
                new NotificationTarget(StudentId: item.StudentId, YearId: item.YearId));
        }

        await db.SaveChangesAsync();
    }

    private async Task ExpireOverdueAsync(short yearId)
    {
        var period = await periods.FindAsync(yearId);
        if (period is null || EnrollmentPeriodService.Today() <= period.PreEnrollmentEnd)
            return;

        var overdue = await db.PreEnrollments
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Where(p => p.YearId == yearId && p.Status == PreEnrollmentStatuses.Reserved)
            .ToListAsync();

        if (overdue.Count == 0)
            return;

        var deadline = period.PreEnrollmentEnd.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var stamp = EnrollmentPeriodService.Today().ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        string KeyOf(PreEnrollment item) => $"{NotificationCodes.SimatCupoLost}:{item.PreEnrollmentId}:{stamp}";

        await notifications.PreloadDedupKeysAsync(overdue.Select(KeyOf));

        foreach (var item in overdue)
        {
            item.Status = PreEnrollmentStatuses.Lost;
            item.ResponseNote = $"La familia no respondió antes del {deadline}.";

            await notifications.QueueAsync(
                NotificationCodes.SimatCupoLost,
                new Dictionary<string, string>
                {
                    ["estudiante"] = FullName(item.Student.User),
                    ["documento"] = item.Student.User.DocumentNumber,
                    ["anio"] = yearId.ToString(),
                    ["fecha"] = deadline,
                },
                new NotificationTarget(StudentId: item.StudentId, YearId: yearId, DedupKey: KeyOf(item)));
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }
    }

    private static string FullName(User user) => $"{user.FirstName} {user.LastName}".Trim();
}