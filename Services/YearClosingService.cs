using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record ClosingClassroomDto(int ClassroomId, string Name, string GradeName, string CampusName, int Active, int Promoted, int Failed);

public record ClosingSubjectDto(int SubjectId, string Name);

public record YearClosingOptionsDto(List<short> Years, short? YearId, List<ClosingClassroomDto> Classrooms, List<ClosingSubjectDto> Subjects);

public record ClosingRowDto(
    int EnrollmentId,
    string StudentName,
    string Document,
    string Status,
    bool HasPiar,
    bool Repeater,
    List<AreaFinal> Areas,
    List<string> LostSubjects,
    decimal AttendancePercent,
    string Proposed,
    string Rule,
    string? DecisionNote,
    string? DecidedBy);

public record ClosingPreviewDto(short YearId, int ClassroomId, string ClassroomName, string GradeName, List<ClosingRowDto> Rows);

public record CloseDecision(int EnrollmentId, string Result, string? Note);

public record CloseRequest(short YearId, int ClassroomId, List<CloseDecision>? Decisions);

public record CloseResultDto(int Promoted, int Failed, int Pending);

public class YearClosingService(AppDbContext db, PromotionSettingsService promotionSettings, AttendanceSettingsService attendanceSettings)
{
    private const int NoteLength = 500;
    private const int ReasonLength = 150;
    private const string PreschoolLevel = "preescolar";

    private static readonly string[] ClosedStatuses = [EnrollmentStatuses.Promoted, EnrollmentStatuses.Failed];

    private sealed record Evaluated(Enrollment Enrollment, PromotionOutcome Outcome, decimal AttendancePercent, bool Repeater);

    public async Task<YearClosingOptionsDto> GetOptionsAsync(short? yearId)
    {
        var years = await db.AcademicYears
            .AsNoTracking()
            .OrderByDescending(y => y.YearId)
            .Select(y => new { y.YearId, y.Active })
            .ToListAsync();

        var selected = yearId is { } requested && years.Any(y => y.YearId == requested)
            ? requested
            : years.Where(y => y.Active).Select(y => (short?)y.YearId).FirstOrDefault() ?? years.Select(y => (short?)y.YearId).FirstOrDefault();

        var subjects = await db.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new ClosingSubjectDto(s.SubjectId, s.Name))
            .ToListAsync();

        if (selected is null)
            return new YearClosingOptionsDto([], null, [], subjects);

        var counts = await db.Enrollments
            .AsNoTracking()
            .Where(e => e.YearId == selected && (e.Status == EnrollmentStatuses.Active || ClosedStatuses.Contains(e.Status)))
            .GroupBy(e => new { e.ClassroomId, e.Status })
            .Select(g => new { g.Key.ClassroomId, g.Key.Status, Count = g.Count() })
            .ToListAsync();

        var classroomIds = counts.Select(c => c.ClassroomId).Distinct().ToList();
        var classrooms = await db.Classrooms
            .AsNoTracking()
            .Where(c => classroomIds.Contains(c.ClassroomId))
            .OrderBy(c => c.Grade.OrderNum)
            .ThenBy(c => c.Name)
            .Select(c => new { c.ClassroomId, c.Name, GradeName = c.Grade.Name, CampusName = c.Campus.Name })
            .ToListAsync();

        int Count(int classroomId, string status) =>
            counts.Where(c => c.ClassroomId == classroomId && c.Status == status).Sum(c => c.Count);

        return new YearClosingOptionsDto(
            years.Select(y => y.YearId).ToList(),
            selected,
            classrooms.Select(c => new ClosingClassroomDto(
                c.ClassroomId,
                c.Name,
                c.GradeName,
                c.CampusName,
                Count(c.ClassroomId, EnrollmentStatuses.Active),
                Count(c.ClassroomId, EnrollmentStatuses.Promoted),
                Count(c.ClassroomId, EnrollmentStatuses.Failed))).ToList(),
            subjects);
    }

    public async Task<ClosingPreviewDto> PreviewAsync(short yearId, int classroomId)
    {
        var classroom = await db.Classrooms
            .AsNoTracking()
            .Include(c => c.Grade)
            .FirstOrDefaultAsync(c => c.ClassroomId == classroomId)
            ?? throw new InvalidOperationException("El curso no existe.");

        var enrollments = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Where(e => e.YearId == yearId && e.ClassroomId == classroomId)
            .Where(e => e.Status == EnrollmentStatuses.Active || ClosedStatuses.Contains(e.Status))
            .ToListAsync();

        var evaluated = await EvaluateAsync(yearId, classroom, enrollments.Where(e => e.Status == EnrollmentStatuses.Active).ToList());
        var repeaters = await RepeatersAsync(yearId, enrollments.Select(e => e.StudentId).ToList());

        var closedIds = enrollments.Where(e => e.Status != EnrollmentStatuses.Active).Select(e => e.EnrollmentId).ToList();
        var closings = (await db.EnrollmentEvents
                .AsNoTracking()
                .Where(v => closedIds.Contains(v.EnrollmentId) && v.Type == EnrollmentEventTypes.YearClose)
                .OrderBy(v => v.EnrollmentEventId)
                .Select(v => new { v.EnrollmentId, v.Note, User = v.User.FirstName + " " + v.User.LastName })
                .ToListAsync())
            .GroupBy(v => v.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.Last());

        var rows = enrollments
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .Select(e =>
            {
                var user = e.Student.User;
                var name = $"{user.LastName}, {user.FirstName}";
                var document = $"{user.DocumentType} {user.DocumentNumber}";
                var repeater = repeaters.Contains(e.StudentId);

                if (evaluated.FirstOrDefault(v => v.Enrollment.EnrollmentId == e.EnrollmentId) is { } open)
                {
                    return new ClosingRowDto(
                        e.EnrollmentId, name, document, e.Status, e.Student.HasPiar, repeater,
                        open.Outcome.Areas, open.Outcome.LostSubjects, open.AttendancePercent,
                        open.Outcome.Result, open.Outcome.Rule, null, null);
                }

                var closing = closings.GetValueOrDefault(e.EnrollmentId);
                return new ClosingRowDto(
                    e.EnrollmentId, name, document, e.Status, e.Student.HasPiar, repeater,
                    [], [], 0m, e.Status, e.StatusReason ?? string.Empty, closing?.Note, closing?.User);
            })
            .ToList();

        return new ClosingPreviewDto(yearId, classroomId, classroom.Name, classroom.Grade.Name, rows);
    }

    public async Task<CloseResultDto> CloseAsync(CloseRequest request, int userId)
    {
        var classroom = await db.Classrooms
            .AsNoTracking()
            .Include(c => c.Grade)
            .FirstOrDefaultAsync(c => c.ClassroomId == request.ClassroomId)
            ?? throw new InvalidOperationException("El curso no existe.");

        var enrollments = await db.Enrollments
            .Include(e => e.Student)
            .Where(e => e.YearId == request.YearId && e.ClassroomId == request.ClassroomId && e.Status == EnrollmentStatuses.Active)
            .ToListAsync();

        var decisions = (request.Decisions ?? []).ToDictionary(d => d.EnrollmentId);
        var evaluated = await EvaluateAsync(request.YearId, classroom, enrollments);

        int promoted = 0, failed = 0, pending = 0;

        foreach (var item in evaluated)
        {
            if (item.Outcome.Result == PromotionRules.Pending)
            {
                pending++;
                continue;
            }

            var result = item.Outcome.Result;
            var rule = item.Outcome.Rule;
            string? note = null;

            if (decisions.TryGetValue(item.Enrollment.EnrollmentId, out var decision) && decision.Result != result)
            {
                if (decision.Result is not (PromotionRules.Promoted or PromotionRules.Failed))
                    throw new InvalidOperationException("La decisión debe ser Promovido o Reprobado.");

                note = Clean(decision.Note, NoteLength);
                if (note is null)
                    throw new InvalidOperationException(
                        "Escriba la justificación de la Comisión de Evaluación y Promoción para cada resultado que cambie.");

                result = decision.Result;
                rule = "Decisión de la Comisión de Evaluación y Promoción (SIEE Art. 13).";
            }

            item.Enrollment.Status = result;
            item.Enrollment.StatusReason = Clean(rule, ReasonLength);

            db.EnrollmentEvents.Add(new EnrollmentEvent
            {
                EnrollmentId = item.Enrollment.EnrollmentId,
                Type = EnrollmentEventTypes.YearClose,
                FromClassroomId = item.Enrollment.ClassroomId,
                Reason = Clean(rule, ReasonLength),
                Note = note,
                UserId = userId,
            });

            if (result == PromotionRules.Promoted) promoted++;
            else failed++;
        }

        await db.SaveChangesAsync();
        return new CloseResultDto(promoted, failed, pending);
    }

    private async Task<List<Evaluated>> EvaluateAsync(short yearId, Classroom classroom, List<Enrollment> enrollments)
    {
        if (enrollments.Count == 0)
            return [];

        var settings = await promotionSettings.GetAsync();
        var preschool = IsPreschool(classroom.Grade);
        var finals = preschool
            ? []
            : await FinalsAsync(yearId, classroom, enrollments.Select(e => e.EnrollmentId).ToList(), settings);

        var attendance = await AttendanceAsync(yearId, classroom.ClassroomId, enrollments.Select(e => e.EnrollmentId).ToList());
        var repeaters = await RepeatersAsync(yearId, enrollments.Select(e => e.StudentId).ToList());

        var result = new List<Evaluated>();

        foreach (var enrollment in enrollments)
        {
            var subjects = finals.GetValueOrDefault(enrollment.EnrollmentId) ?? [];
            var percent = attendance.GetValueOrDefault(enrollment.EnrollmentId, 100m);

            PromotionOutcome Run(IReadOnlyList<SubjectFinal> list) => PromotionRules.Evaluate(
                new PromotionInput(classroom.Grade.OrderNum, preschool, enrollment.Student.HasPiar, list, percent),
                settings);

            var outcome = Run(subjects);

            if (outcome.UsedException)
            {
                var lostBefore = await LostLastYearAsync(yearId, enrollment.StudentId, settings);
                if (lostBefore.Count > 0)
                    outcome = Run(subjects.Select(s => s with { LostLastYear = lostBefore.Contains(s.SubjectId) }).ToList());
            }

            result.Add(new Evaluated(enrollment, outcome, percent, repeaters.Contains(enrollment.StudentId)));
        }

        return result;
    }

    private async Task<Dictionary<int, List<SubjectFinal>>> FinalsAsync(
        short yearId, Classroom classroom, List<int> enrollmentIds, PromotionSettings settings)
    {
        var periods = await db.AcademicPeriods
            .AsNoTracking()
            .Where(p => p.YearId == yearId)
            .OrderBy(p => p.PeriodNumber)
            .ToListAsync();

        if (periods.Count != 3 || !periods.Select(p => p.PeriodNumber).SequenceEqual(new byte[] { 1, 2, 3 }))
            throw new InvalidOperationException(
                $"El año lectivo {yearId} debe tener configurados los tres periodos académicos (1, 2 y 3) que define el SIEE.");

        var weights = new[] { settings.Period1Weight, settings.Period2Weight, settings.Period3Weight };
        var periodIds = periods.Select(p => p.PeriodId).ToList();

        var subjects = (await db.AcademicAssignments
                .AsNoTracking()
                .Where(a => a.Active && a.YearId == yearId && a.ClassroomId == classroom.ClassroomId)
                .Select(a => new { a.SubjectId, a.Subject.Name, a.Subject.KnowledgeArea, a.Subject.WeeklyHours })
                .ToListAsync())
            .DistinctBy(s => s.SubjectId)
            .OrderBy(s => s.Name)
            .ToList();
        var subjectIds = subjects.Select(s => s.SubjectId).ToList();

        var planHours = (await db.StudyPlans
                .AsNoTracking()
                .Where(p => p.YearId == yearId && p.GradeId == classroom.GradeId && subjectIds.Contains(p.SubjectId))
                .Select(p => new { p.SubjectId, p.WeeklyHours })
                .ToListAsync())
            .GroupBy(p => p.SubjectId)
            .ToDictionary(g => g.Key, g => (decimal)g.First().WeeklyHours);

        var schemes = await db.GradingSchemes
            .AsNoTracking()
            .Include(s => s.Activities)
            .Where(s => subjectIds.Contains(s.SubjectId) && periodIds.Contains(s.PeriodId) && s.Classroom.GradeId == classroom.GradeId)
            .ToListAsync();
        var activityIds = schemes.SelectMany(s => s.Activities).Select(a => a.GradeActivityId).ToList();

        var scores = (await db.ActivityScores
                .AsNoTracking()
                .Where(s => activityIds.Contains(s.GradeActivityId) && enrollmentIds.Contains(s.EnrollmentId))
                .Select(s => new { s.EnrollmentId, s.GradeActivityId, s.Value })
                .ToListAsync())
            .GroupBy(s => s.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(s => s.GradeActivityId, s => s.Value));

        var losses = (await db.AbsenceLosses
                .AsNoTracking()
                .Where(l => enrollmentIds.Contains(l.EnrollmentId) && periodIds.Contains(l.PeriodId))
                .Select(l => new { l.EnrollmentId, l.SubjectId, l.PeriodId })
                .ToListAsync())
            .Select(l => (l.EnrollmentId, l.SubjectId, l.PeriodId))
            .ToHashSet();
        var lossGrade = losses.Count > 0 ? (await attendanceSettings.GetAsync()).LossGrade : 0m;

        var earlyPromoted = (await db.EnrollmentEvents
                .AsNoTracking()
                .Where(v => enrollmentIds.Contains(v.EnrollmentId) && v.Type == EnrollmentEventTypes.EarlyPromotion)
                .Select(v => v.EnrollmentId)
                .ToListAsync())
            .ToHashSet();

        var result = new Dictionary<int, List<SubjectFinal>>();

        foreach (var enrollmentId in enrollmentIds)
        {
            var values = scores.GetValueOrDefault(enrollmentId) ?? [];

            decimal? PeriodFinal(int subjectId, short periodId)
            {
                var candidates = schemes.Where(s => s.SubjectId == subjectId && s.PeriodId == periodId).ToList();
                int Graded(GradingScheme s) => s.Activities.Count(a => values.ContainsKey(a.GradeActivityId));

                var own = candidates.FirstOrDefault(s => s.ClassroomId == classroom.ClassroomId);
                var scheme = own is not null && Graded(own) > 0
                    ? own
                    : candidates.Where(s => Graded(s) > 0).OrderByDescending(Graded).FirstOrDefault() ?? own;

                decimal? loss = losses.Contains((enrollmentId, subjectId, periodId)) ? lossGrade : null;
                if (scheme is null)
                    return loss is { } only ? GradingRules.Round(only) : null;

                decimal? Value(string component) =>
                    scheme.Activities.FirstOrDefault(a => a.Component == component) is { } activity &&
                    values.TryGetValue(activity.GradeActivityId, out var v) ? v : null;

                var hetero = scheme.Activities
                    .Where(a => a.Component == GradeComponents.Hetero)
                    .Select(a => new HeteroScore(a.Weight, values.TryGetValue(a.GradeActivityId, out var v) ? v : null))
                    .ToList();

                return GradingRules.Compute(
                    new PeriodScores(
                        scheme.Mode == GradingModes.Weighted ? GradingMode.Weighted : GradingMode.Average,
                        hetero,
                        Value(GradeComponents.Saber),
                        Value(GradeComponents.Self),
                        Value(GradeComponents.Peer)),
                    loss).Final;
            }

            result[enrollmentId] = subjects.Select(subject =>
            {
                var byPeriod = periods.Select(p => PeriodFinal(subject.SubjectId, p.PeriodId)).ToArray();

                if (earlyPromoted.Contains(enrollmentId) && byPeriod[1] is { } second)
                    byPeriod[0] = byPeriod[0] is { } first ? GradingRules.Round((first + second) / 2m) : second;

                var final = PromotionRules.YearFinal(byPeriod.Select((value, index) => (weights[index], value)).ToList());
                var hours = planHours.GetValueOrDefault(subject.SubjectId, subject.WeeklyHours);
                return new SubjectFinal(subject.SubjectId, subject.Name, subject.KnowledgeArea, hours, final);
            }).ToList();
        }

        return result;
    }

    private async Task<Dictionary<int, decimal>> AttendanceAsync(short yearId, int classroomId, List<int> enrollmentIds)
    {
        var sessions = await db.AttendanceSessions.CountAsync(s => s.YearId == yearId && s.ClassroomId == classroomId);
        if (sessions == 0)
            return [];

        var absences = await db.AttendanceAbsences
            .AsNoTracking()
            .Where(a => enrollmentIds.Contains(a.EnrollmentId) && a.Session.YearId == yearId)
            .GroupBy(a => a.EnrollmentId)
            .Select(g => new { EnrollmentId = g.Key, Count = g.Count() })
            .ToListAsync();

        return absences.ToDictionary(
            a => a.EnrollmentId,
            a => GradingRules.Round(Math.Max(0m, 100m * (1m - (decimal)a.Count / sessions))));
    }

    private async Task<HashSet<int>> RepeatersAsync(short yearId, List<int> studentIds)
    {
        var previous = await db.Enrollments
            .AsNoTracking()
            .Where(e => studentIds.Contains(e.StudentId) && e.YearId < yearId)
            .Select(e => new { e.StudentId, e.YearId, e.Status })
            .ToListAsync();

        return previous
            .GroupBy(e => e.StudentId)
            .Where(g => g.OrderByDescending(e => e.YearId).First().Status == EnrollmentStatuses.Failed)
            .Select(g => g.Key)
            .ToHashSet();
    }

    private async Task<HashSet<int>> LostLastYearAsync(short yearId, int studentId, PromotionSettings settings)
    {
        var previous = await db.Enrollments
            .AsNoTracking()
            .Include(e => e.Classroom).ThenInclude(c => c.Grade)
            .Where(e => e.StudentId == studentId && e.YearId < yearId)
            .OrderByDescending(e => e.YearId)
            .FirstOrDefaultAsync();

        if (previous is null)
            return [];

        try
        {
            var finals = await FinalsAsync(previous.YearId, previous.Classroom, [previous.EnrollmentId], settings);
            return finals[previous.EnrollmentId]
                .Where(s => s.Final is { } final && !GradingRules.Passes(final))
                .Select(s => s.SubjectId)
                .ToHashSet();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private static bool IsPreschool(Grade grade) =>
        grade.OrderNum < 1 || grade.Level.Trim().StartsWith(PreschoolLevel, StringComparison.OrdinalIgnoreCase);

    private static string? Clean(string? text, int length)
    {
        var clean = text?.Trim();
        if (string.IsNullOrEmpty(clean))
            return null;

        return clean.Length > length ? clean[..length] : clean;
    }
}
