using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record GradingAccess(int UserId, PermissionFlags GradeBook, PermissionFlags GradeDesign, PermissionFlags StudyPlanner);

public record GradingPeriodDto(short PeriodId, byte PeriodNumber, DateOnly StartDate, DateOnly EndDate, bool Closed, bool DesignLocked);

public record GradingCourseDto(int ClassroomId, string ClassroomName, int SubjectId, string SubjectName, bool Own);

public record GradingOptionsDto(short? YearId, List<GradingPeriodDto> Periods, List<GradingCourseDto> Courses, short? CurrentPeriodId);

public record ActivityDto(int ActivityId, string Component, string Name, string? Description, string? Competencies, decimal? Weight, int DisplayOrder);

public record StudentRowDto(
    int EnrollmentId,
    string StudentName,
    Dictionary<int, decimal> Scores,
    decimal? Accumulated,
    decimal? Final,
    bool Complete,
    bool AbsenceLoss,
    string? Performance,
    bool? Passing);

public record GradeSheetDto(
    int ClassroomId,
    int SubjectId,
    short PeriodId,
    string Mode,
    decimal WeightSum,
    bool PeriodClosed,
    bool DesignLocked,
    bool CanEditDesign,
    bool CanEditScores,
    List<ActivityDto> Activities,
    List<StudentRowDto> Students);

public record SetModeRequest(int ClassroomId, int SubjectId, short PeriodId, string Mode);

public record SaveActivityRequest(
    int? ActivityId,
    int ClassroomId,
    int SubjectId,
    short PeriodId,
    string Name,
    string? Description,
    string? Competencies,
    decimal? Weight);

public record ScoreInput(int EnrollmentId, int ActivityId, decimal? Value);

public record SaveScoresRequest(int ClassroomId, int SubjectId, short PeriodId, List<ScoreInput> Scores);

public class GradebookService(AppDbContext db, AttendanceSettingsService settings, GradingDeadlineService deadlines)
{
    private const int NameLength = 150;
    private const int TextLength = 1000;
    private const int FixedOrder = 1000;

    private static readonly (string Component, string Name)[] FixedComponents =
    [
        (GradeComponents.Saber, "Prueba Saber"),
        (GradeComponents.Self, "Autoevaluación"),
        (GradeComponents.Peer, "Coevaluación"),
    ];

    private sealed record SheetContext(AcademicPeriod Period, GradingScheme Scheme, bool Own, bool Closed, bool Locked);

    public async Task<GradingOptionsDto> GetOptionsAsync(GradingAccess access, DateTime nowUtc, DateOnly today)
    {
        var year = await deadlines.CurrentYearAsync(nowUtc);

        if (year is null)
            return new GradingOptionsDto(null, [], [], null);

        var periods = await db.AcademicPeriods
            .AsNoTracking()
            .Where(p => p.YearId == year.YearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();

        var locks = (await deadlines.GetAsync(year.YearId)).Periods
            .ToDictionary(p => p.PeriodId, p => p.DesignLockAt);

        var periodDtos = periods.Select(p => new GradingPeriodDto(
            p.PeriodId,
            p.PeriodNumber,
            AttendancePeriods.Start(p),
            AttendancePeriods.End(p),
            GradingRules.IsPeriodClosed(p, today),
            GradingRules.IsDesignLocked(locks.GetValueOrDefault(p.PeriodId), nowUtc))).ToList();

        var current = periods.FirstOrDefault(p => AttendancePeriods.Contains(p, today))
            ?? periods.LastOrDefault(p => AttendancePeriods.Start(p) <= today)
            ?? periods.FirstOrDefault();

        var seeAll = access.GradeBook.HasFlag(PermissionFlags.Update);

        var assignments = await db.AcademicAssignments
            .AsNoTracking()
            .Where(a => a.Active && a.YearId == year.YearId && a.Classroom.Active)
            .Where(a => seeAll || a.TeacherId == access.UserId)
            .Select(a => new
            {
                a.ClassroomId,
                ClassroomName = a.Classroom.Name,
                a.SubjectId,
                SubjectName = a.Subject.Name,
                Own = a.TeacherId == access.UserId,
            })
            .ToListAsync();

        var courses = assignments
            .GroupBy(a => new { a.ClassroomId, a.SubjectId })
            .Select(g => new GradingCourseDto(
                g.Key.ClassroomId,
                g.First().ClassroomName,
                g.Key.SubjectId,
                g.First().SubjectName,
                g.Any(a => a.Own)))
            .OrderBy(c => c.ClassroomName)
            .ThenBy(c => c.SubjectName)
            .ToList();

        return new GradingOptionsDto(year.YearId, periodDtos, courses, current?.PeriodId);
    }

    public async Task<GradeSheetDto> GetSheetAsync(GradingAccess access, int classroomId, int subjectId, short periodId, DateTime nowUtc, DateOnly today)
    {
        var context = await LoadAsync(access, classroomId, subjectId, periodId, nowUtc, today);
        return await BuildAsync(access, context);
    }

    public async Task<GradeSheetDto> SetModeAsync(GradingAccess access, SetModeRequest request, DateTime nowUtc, DateOnly today)
    {
        if (request.Mode is not (GradingModes.Weighted or GradingModes.Average))
            throw new InvalidOperationException("Modo de calificación no válido.");

        var context = await LoadAsync(access, request.ClassroomId, request.SubjectId, request.PeriodId, nowUtc, today);
        EnsureDesign(access, context, PermissionFlags.Create);

        if (context.Scheme.Mode != request.Mode && request.Mode == GradingModes.Weighted)
        {
            foreach (var activity in context.Scheme.Activities.Where(a => a.Component == GradeComponents.Hetero))
                activity.Weight = null;
        }

        context.Scheme.Mode = request.Mode;
        context.Scheme.UpdatedById = access.UserId;
        context.Scheme.UpdatedAt = nowUtc;
        await db.SaveChangesAsync();

        return await BuildAsync(access, context);
    }

    public async Task<GradeSheetDto> SaveActivityAsync(GradingAccess access, SaveActivityRequest request, DateTime nowUtc, DateOnly today)
    {
        var context = await LoadAsync(access, request.ClassroomId, request.SubjectId, request.PeriodId, nowUtc, today);
        EnsureDesign(access, context, PermissionFlags.Create);

        var name = request.Name?.Trim() ?? "";
        if (name.Length == 0)
            throw new InvalidOperationException("El nombre de la actividad es obligatorio.");
        if (name.Length > NameLength)
            throw new InvalidOperationException($"El nombre admite máximo {NameLength} caracteres.");

        var description = Clean(request.Description, "La descripción");
        var competencies = Clean(request.Competencies, "Las competencias");
        var weighted = context.Scheme.Mode == GradingModes.Weighted;

        if (weighted && request.Weight is { } weight && (weight <= 0m || weight > 100m || decimal.Round(weight, 2) != weight))
            throw new InvalidOperationException("El porcentaje debe ser mayor que 0 y máximo 100, con dos decimales.");

        GradeActivity activity;

        if (request.ActivityId is { } id)
        {
            activity = context.Scheme.Activities.FirstOrDefault(a => a.GradeActivityId == id)
                ?? throw new InvalidOperationException("La actividad no existe.");

            if (activity.Component != GradeComponents.Hetero)
                throw new InvalidOperationException("Este componente no se puede modificar.");
        }
        else
        {
            activity = new GradeActivity
            {
                GradingSchemeId = context.Scheme.GradingSchemeId,
                Component = GradeComponents.Hetero,
                CreatedById = access.UserId,
                CreatedAt = nowUtc,
                DisplayOrder = context.Scheme.Activities
                    .Where(a => a.Component == GradeComponents.Hetero)
                    .Select(a => a.DisplayOrder)
                    .DefaultIfEmpty(0)
                    .Max() + 1,
            };
            context.Scheme.Activities.Add(activity);
        }

        activity.Name = name;
        activity.Description = description;
        activity.Competencies = competencies;

        if (weighted)
            activity.Weight = request.Weight;

        Touch(context.Scheme, access.UserId, nowUtc);
        await db.SaveChangesAsync();

        return await BuildAsync(access, context);
    }

    public async Task<GradeSheetDto> DeleteActivityAsync(GradingAccess access, int activityId, DateTime nowUtc, DateOnly today)
    {
        var target = await db.GradeActivities
            .AsNoTracking()
            .Where(a => a.GradeActivityId == activityId)
            .Select(a => new { a.Component, a.GradingScheme.ClassroomId, a.GradingScheme.SubjectId, a.GradingScheme.PeriodId })
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("La actividad no existe.");

        if (target.Component != GradeComponents.Hetero)
            throw new InvalidOperationException("Este componente no se puede modificar.");

        var context = await LoadAsync(access, target.ClassroomId, target.SubjectId, target.PeriodId, nowUtc, today);
        EnsureDesign(access, context, PermissionFlags.Delete);

        var activity = context.Scheme.Activities.Single(a => a.GradeActivityId == activityId);

        db.ActivityScores.RemoveRange(await db.ActivityScores.Where(s => s.GradeActivityId == activityId).ToListAsync());
        context.Scheme.Activities.Remove(activity);
        db.GradeActivities.Remove(activity);
        Touch(context.Scheme, access.UserId, nowUtc);
        await db.SaveChangesAsync();

        return await BuildAsync(access, context);
    }

    public async Task<GradeSheetDto> SaveScoresAsync(GradingAccess access, SaveScoresRequest request, DateTime nowUtc, DateOnly today)
    {
        var context = await LoadAsync(access, request.ClassroomId, request.SubjectId, request.PeriodId, nowUtc, today);

        if (!CanEditScores(access, context))
            throw new UnauthorizedAccessException();

        var activityIds = context.Scheme.Activities.Select(a => a.GradeActivityId).ToHashSet();
        var enrollmentIds = (await RosterQuery(context).Select(e => e.EnrollmentId).ToListAsync()).ToHashSet();

        foreach (var score in request.Scores)
        {
            if (!activityIds.Contains(score.ActivityId))
                throw new InvalidOperationException("La actividad no pertenece a esta planilla.");
            if (!enrollmentIds.Contains(score.EnrollmentId))
                throw new InvalidOperationException("El estudiante no pertenece a este curso.");
            if (score.Value is { } value && !GradingRules.IsValidRaw(value))
                throw new InvalidOperationException("Las notas deben estar entre 10.0 y 50.0, con un decimal.");
        }

        var pairs = request.Scores.Select(s => (s.ActivityId, s.EnrollmentId)).ToList();
        if (pairs.Distinct().Count() != pairs.Count)
            throw new InvalidOperationException("Hay notas repetidas en la solicitud.");

        var touchedActivities = request.Scores.Select(s => s.ActivityId).Distinct().ToList();
        var existing = await db.ActivityScores
            .Where(s => touchedActivities.Contains(s.GradeActivityId))
            .ToListAsync();

        foreach (var input in request.Scores)
        {
            var row = existing.FirstOrDefault(s => s.GradeActivityId == input.ActivityId && s.EnrollmentId == input.EnrollmentId);

            if (input.Value is null)
            {
                if (row is not null)
                    db.ActivityScores.Remove(row);
                continue;
            }

            if (row is null)
            {
                db.ActivityScores.Add(new ActivityScore
                {
                    GradeActivityId = input.ActivityId,
                    EnrollmentId = input.EnrollmentId,
                    Value = input.Value.Value,
                    RecordedById = access.UserId,
                    RecordedAt = nowUtc,
                });
            }
            else if (row.Value != input.Value.Value)
            {
                row.Value = input.Value.Value;
                row.UpdatedById = access.UserId;
                row.UpdatedAt = nowUtc;
            }
        }

        await db.SaveChangesAsync();

        return await BuildAsync(access, context);
    }

    private async Task<SheetContext> LoadAsync(GradingAccess access, int classroomId, int subjectId, short periodId, DateTime nowUtc, DateOnly today)
    {
        var period = await db.AcademicPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.PeriodId == periodId)
            ?? throw new InvalidOperationException("El periodo no existe.");

        var checks = await db.Classrooms
            .Where(c => c.ClassroomId == classroomId)
            .Select(c => new
            {
                SubjectExists = db.Subjects.Any(s => s.SubjectId == subjectId),
                Own = db.AcademicAssignments.Any(a =>
                    a.Active &&
                    a.YearId == period.YearId &&
                    a.ClassroomId == classroomId &&
                    a.SubjectId == subjectId &&
                    a.TeacherId == access.UserId),
            })
            .FirstOrDefaultAsync();

        if (checks is null || !checks.SubjectExists)
            throw new InvalidOperationException("El curso o la materia no existen.");

        var own = checks.Own;

        var canRead = (own && access.GradeBook.HasFlag(PermissionFlags.Read)) || access.GradeBook.HasFlag(PermissionFlags.Update);
        var canDesign = (own && (access.GradeDesign & (PermissionFlags.Create | PermissionFlags.Delete)) != 0) || access.GradeDesign.HasFlag(PermissionFlags.Update);

        if (!canRead && !canDesign)
            throw new UnauthorizedAccessException();

        var scheme = await db.GradingSchemes
            .Include(s => s.Activities)
            .FirstOrDefaultAsync(s => s.ClassroomId == classroomId && s.SubjectId == subjectId && s.PeriodId == periodId);

        if (scheme is null)
        {
            scheme = new GradingScheme
            {
                ClassroomId = classroomId,
                SubjectId = subjectId,
                PeriodId = periodId,
                Mode = GradingModes.Average,
                UpdatedById = access.UserId,
                UpdatedAt = nowUtc,
            };

            for (var i = 0; i < FixedComponents.Length; i++)
            {
                scheme.Activities.Add(new GradeActivity
                {
                    Component = FixedComponents[i].Component,
                    Name = FixedComponents[i].Name,
                    DisplayOrder = FixedOrder + i + 1,
                    CreatedById = access.UserId,
                    CreatedAt = nowUtc,
                });
            }

            db.GradingSchemes.Add(scheme);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();

                scheme = await db.GradingSchemes
                    .Include(s => s.Activities)
                    .FirstOrDefaultAsync(s => s.ClassroomId == classroomId && s.SubjectId == subjectId && s.PeriodId == periodId)
                    ?? throw new InvalidOperationException("No fue posible preparar la planilla. Intente de nuevo.");
            }
        }

        var locked = GradingRules.IsDesignLocked(await deadlines.DesignLockAsync(periodId), nowUtc);

        return new SheetContext(period, scheme, own, GradingRules.IsPeriodClosed(period, today), locked);
    }

    private IQueryable<Enrollment> RosterQuery(SheetContext context) =>
        db.Enrollments.Where(e =>
            e.ClassroomId == context.Scheme.ClassroomId &&
            e.YearId == context.Period.YearId &&
            e.Status == EnrollmentStatuses.Active);

    private async Task<GradeSheetDto> BuildAsync(GradingAccess access, SheetContext context)
    {
        var scheme = context.Scheme;
        var activities = scheme.Activities
            .OrderBy(a => a.Component == GradeComponents.Hetero ? 0 : 1)
            .ThenBy(a => a.DisplayOrder)
            .ThenBy(a => a.GradeActivityId)
            .ToList();
        var activityIds = activities.Select(a => a.GradeActivityId).ToList();

        var students = await RosterQuery(context)
            .AsNoTracking()
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .Select(e => new { e.EnrollmentId, e.Student.User.FirstName, e.Student.User.LastName })
            .ToListAsync();
        var enrollmentIds = students.Select(s => s.EnrollmentId).ToList();

        var scores = await db.ActivityScores
            .AsNoTracking()
            .Where(s => activityIds.Contains(s.GradeActivityId) && enrollmentIds.Contains(s.EnrollmentId))
            .Select(s => new { s.EnrollmentId, s.GradeActivityId, s.Value })
            .ToListAsync();

        var losses = (await db.AbsenceLosses
            .AsNoTracking()
            .Where(l => l.SubjectId == scheme.SubjectId && l.PeriodId == scheme.PeriodId && enrollmentIds.Contains(l.EnrollmentId))
            .Select(l => l.EnrollmentId)
            .ToListAsync()).ToHashSet();

        var lossGrade = losses.Count > 0 ? (await settings.GetAsync()).LossGrade : 0m;
        var mode = scheme.Mode == GradingModes.Weighted ? GradingMode.Weighted : GradingMode.Average;
        var hetero = activities.Where(a => a.Component == GradeComponents.Hetero).ToList();
        int? Fixed(string component) => activities.FirstOrDefault(a => a.Component == component)?.GradeActivityId;

        var byStudent = scores
            .GroupBy(s => s.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(s => s.GradeActivityId, s => s.Value));

        var rows = students.Select(student =>
        {
            var values = byStudent.GetValueOrDefault(student.EnrollmentId) ?? [];
            decimal? Value(int? activityId) =>
                activityId is { } id && values.TryGetValue(id, out var v) ? v : null;

            var result = GradingRules.Compute(
                new PeriodScores(
                    mode,
                    hetero.Select(a => new HeteroScore(a.Weight, Value(a.GradeActivityId))).ToList(),
                    Value(Fixed(GradeComponents.Saber)),
                    Value(Fixed(GradeComponents.Self)),
                    Value(Fixed(GradeComponents.Peer))),
                losses.Contains(student.EnrollmentId) ? lossGrade : null);

            return new StudentRowDto(
                student.EnrollmentId,
                $"{student.LastName}, {student.FirstName}",
                values,
                result.Accumulated,
                result.Final,
                result.Complete,
                result.AbsenceLoss,
                result.Performance,
                result.Passing);
        }).ToList();

        return new GradeSheetDto(
            scheme.ClassroomId,
            scheme.SubjectId,
            scheme.PeriodId,
            scheme.Mode,
            GradingRules.WeightSum(hetero.Select(a => new HeteroScore(a.Weight, null))),
            context.Closed,
            context.Locked,
            CanEditDesign(access, context, PermissionFlags.Create),
            CanEditScores(access, context),
            activities.Select(a => new ActivityDto(a.GradeActivityId, a.Component, a.Name, a.Description, a.Competencies, a.Weight, a.DisplayOrder)).ToList(),
            rows);
    }

    private static bool CanEditScores(GradingAccess access, SheetContext context) =>
        (context.Own && access.GradeBook.HasFlag(PermissionFlags.Create) && !context.Closed) ||
        access.GradeBook.HasFlag(PermissionFlags.Update);

    private static bool CanEditDesign(GradingAccess access, SheetContext context, PermissionFlags flag) =>
        (context.Own && access.GradeDesign.HasFlag(flag) && !context.Locked) ||
        access.GradeDesign.HasFlag(PermissionFlags.Update);

    private static void EnsureDesign(GradingAccess access, SheetContext context, PermissionFlags flag)
    {
        if (!CanEditDesign(access, context, flag))
            throw new UnauthorizedAccessException();
    }

    private static void Touch(GradingScheme scheme, int userId, DateTime nowUtc)
    {
        scheme.UpdatedById = userId;
        scheme.UpdatedAt = nowUtc;
    }

    private static string? Clean(string? value, string label)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
            return null;

        return text.Length <= TextLength
            ? text
            : throw new InvalidOperationException($"{label} admite máximo {TextLength} caracteres.");
    }
}
