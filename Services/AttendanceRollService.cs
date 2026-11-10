using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record AgendaItemDto(
    int ClassroomId,
    string ClassroomName,
    string Kind,
    int? SubjectId,
    string? SubjectName,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool Taken);

public record SheetStudentDto(
    int EnrollmentId,
    string Name,
    string? Status,
    bool Justified,
    string? EarlierClass,
    bool Late,
    string? TardyObservation);

public record SheetDto(
    int? SessionId,
    int ClassroomId,
    string ClassroomName,
    DateOnly Date,
    string Kind,
    int? SubjectId,
    string? SubjectName,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    bool FirstOfDay,
    bool CanEdit,
    List<SheetStudentDto> Students);

public record TardyEntry(int EnrollmentId, string? Observation);

public record SaveSheetRequest(
    int ClassroomId,
    DateOnly Date,
    TimeOnly? StartTime,
    List<int>? AbsentEnrollmentIds,
    List<TardyEntry>? Tardies);

public record SaveSheetResult(int SessionId, int Absences, int Evasions, int Tardies);

public record RollAccess(int UserId, bool CanCreate, bool CanEditAny);

public class AttendanceRollService(
    AppDbContext db,
    AttendanceSettingsService settings,
    NotificationService notifications,
    LateArrivalService lateArrivals,
    AttendanceExcuseService excuses)
{
    private const string NoPeriodMessage = "La fecha no pertenece a ningún periodo académico.";
    private const string NoClassMessage = "No hay una clase en ese horario.";
    private const int ObservationLength = 500;

    private sealed record ClassroomInfo(int ClassroomId, string Name, int CampusId, string Level, int? DirectorTeacherId);

    private sealed record Slot(
        string Kind,
        int? AssignmentId,
        int? SubjectId,
        string? SubjectName,
        TimeOnly? Start,
        TimeOnly? End,
        bool Owned,
        AttendanceSession? Session);

    private sealed record RosterEntry(int EnrollmentId, int StudentId, string FirstName, string LastName, bool Active);

    public async Task<List<AgendaItemDto>> GetAgendaAsync(RollAccess access, DateOnly date, int? classroomId)
    {
        if (classroomId is not null && !access.CanEditAny)
            throw new UnauthorizedAccessException();

        var period = await AttendancePeriods.FindByDateAsync(db, date);
        if (period is null)
            return [];

        var yearId = period.YearId;
        var weekday = AttendanceRules.WeekdayOf(date);
        var dailyLevels = (await settings.GetAsync()).DailyLevels;

        var blocks = await db.Schedules
            .AsNoTracking()
            .Where(s => s.YearId == yearId && s.Type == ScheduleTypes.Class && s.Weekday == weekday && s.ClassroomId != null)
            .Where(s => classroomId != null ? s.ClassroomId == classroomId : s.TeacherId == access.UserId)
            .Select(s => new
            {
                ClassroomId = s.ClassroomId!.Value,
                ClassroomName = s.Classroom!.Name,
                s.Classroom.Grade.Level,
                SubjectId = (int?)s.AcademicAssignment!.SubjectId,
                SubjectName = s.AcademicAssignment.Subject.Name,
                s.StartTime,
                s.EndTime,
            })
            .ToListAsync();

        var dailyClassrooms = await DailyClassroomsAsync(access.UserId, classroomId, yearId, weekday, dailyLevels);
        var dailyIds = dailyClassrooms.Select(c => c.ClassroomId).ToHashSet();

        var sessions = await db.AttendanceSessions
            .AsNoTracking()
            .Where(s => s.Date == date)
            .Where(s => classroomId != null
                ? s.ClassroomId == classroomId
                : (s.Kind == AttendanceSessionKinds.Class && s.AcademicAssignment!.TeacherId == access.UserId) ||
                  (s.Kind == AttendanceSessionKinds.Daily && dailyIds.Contains(s.ClassroomId)))
            .Select(s => new
            {
                s.ClassroomId,
                ClassroomName = s.Classroom.Name,
                s.Kind,
                s.SubjectId,
                SubjectName = s.Subject != null ? s.Subject.Name : null,
                s.StartTime,
                s.EndTime,
            })
            .ToListAsync();

        var items = new List<AgendaItemDto>();

        foreach (var block in blocks.Where(b => !AttendanceRules.IsDailyLevel(b.Level, dailyLevels)))
        {
            var taken = sessions.Any(s =>
                s.Kind == AttendanceSessionKinds.Class && s.ClassroomId == block.ClassroomId && s.StartTime == block.StartTime);

            items.Add(new AgendaItemDto(
                block.ClassroomId, block.ClassroomName, AttendanceSessionKinds.Class,
                block.SubjectId, block.SubjectName, block.StartTime, block.EndTime, taken));
        }

        foreach (var classroom in dailyClassrooms)
        {
            var taken = sessions.Any(s => s.Kind == AttendanceSessionKinds.Daily && s.ClassroomId == classroom.ClassroomId);

            items.Add(new AgendaItemDto(
                classroom.ClassroomId, classroom.Name, AttendanceSessionKinds.Daily, null, null, null, null, taken));
        }

        foreach (var session in sessions.Where(s => s.Kind == AttendanceSessionKinds.Class))
        {
            if (items.Any(i => i.ClassroomId == session.ClassroomId && i.StartTime == session.StartTime))
                continue;

            items.Add(new AgendaItemDto(
                session.ClassroomId, session.ClassroomName, AttendanceSessionKinds.Class,
                session.SubjectId, session.SubjectName, session.StartTime, session.EndTime, true));
        }

        return items
            .OrderBy(i => i.StartTime ?? TimeOnly.MinValue)
            .ThenBy(i => i.ClassroomName)
            .ToList();
    }

    public async Task<SheetDto> GetSheetAsync(RollAccess access, int classroomId, DateOnly date, TimeOnly? startTime, DateOnly today)
    {
        var classroom = await LoadClassroomAsync(classroomId);
        var period = await AttendancePeriods.FindByDateAsync(db, date)
            ?? throw new InvalidOperationException(NoPeriodMessage);

        var slot = await ResolveSlotAsync(access.UserId, classroom, period.YearId, date, startTime);

        if (!access.CanEditAny && !slot.Owned)
            throw new UnauthorizedAccessException();

        var roster = await LoadRosterAsync(classroomId, period.YearId);
        var sessionId = slot.Session?.AttendanceSessionId;

        var absences = sessionId is null
            ? []
            : await db.AttendanceAbsences.AsNoTracking().Where(a => a.AttendanceSessionId == sessionId).ToListAsync();

        var tardies = sessionId is null
            ? []
            : await db.ClassTardinesses.AsNoTracking().Where(t => t.AttendanceSessionId == sessionId).ToListAsync();

        var isClass = slot.Kind == AttendanceSessionKinds.Class;

        var earlier = isClass
            ? await db.AttendanceSessions
                .AsNoTracking()
                .Where(s => s.ClassroomId == classroomId && s.Date == date &&
                            s.Kind == AttendanceSessionKinds.Class && s.StartTime < slot.Start)
                .OrderByDescending(s => s.StartTime)
                .Select(s => new
                {
                    s.AttendanceSessionId,
                    s.StartTime,
                    SubjectName = s.Subject!.Name,
                    Absent = db.AttendanceAbsences
                        .Where(a => a.AttendanceSessionId == s.AttendanceSessionId)
                        .Select(a => a.EnrollmentId)
                        .ToList(),
                })
                .ToListAsync()
            : [];

        var lateToday = isClass
            ? []
            : await db.LateArrivals
                .Where(l => l.Date == date && l.Enrollment.ClassroomId == classroomId)
                .Select(l => l.EnrollmentId)
                .ToListAsync();

        var students = roster
            .Where(r => r.Active || absences.Any(a => a.EnrollmentId == r.EnrollmentId))
            .Select(r =>
            {
                var absence = absences.FirstOrDefault(a => a.EnrollmentId == r.EnrollmentId);
                var tardy = tardies.FirstOrDefault(t => t.EnrollmentId == r.EnrollmentId);
                var attended = earlier.FirstOrDefault(e => !e.Absent.Contains(r.EnrollmentId));

                return new SheetStudentDto(
                    r.EnrollmentId,
                    $"{r.LastName}, {r.FirstName}",
                    absence?.Status,
                    absence?.AttendanceExcuseId is not null,
                    attended is null ? null : $"{attended.SubjectName} a las {attended.StartTime:H:mm}",
                    isClass ? tardy is not null : lateToday.Contains(r.EnrollmentId),
                    tardy?.Observation);
            })
            .ToList();

        return new SheetDto(
            sessionId,
            classroom.ClassroomId,
            classroom.Name,
            date,
            slot.Kind,
            slot.SubjectId,
            slot.SubjectName,
            slot.Start,
            slot.End,
            isClass && await IsFirstOfDayAsync(classroomId, period.YearId, date, slot.Start!.Value),
            access.CanEditAny || (access.CanCreate && slot.Owned && date == today),
            students);
    }

    public async Task<SaveSheetResult> SaveSheetAsync(RollAccess access, SaveSheetRequest request, DateOnly today)
    {
        var classroom = await LoadClassroomAsync(request.ClassroomId);
        var date = request.Date;

        if (date > today)
            throw new InvalidOperationException("No se puede tomar lista de una fecha futura.");

        var period = await AttendancePeriods.FindByDateAsync(db, date)
            ?? throw new InvalidOperationException(NoPeriodMessage);
        var yearId = period.YearId;

        var slot = await ResolveSlotAsync(access.UserId, classroom, yearId, date, request.StartTime);

        if (!access.CanEditAny)
        {
            if (!access.CanCreate || !slot.Owned)
                throw new UnauthorizedAccessException();

            if (date != today)
                throw new InvalidOperationException("Solo puedes tomar o cambiar la lista del día de hoy.");
        }

        var isClass = slot.Kind == AttendanceSessionKinds.Class;
        var absentIds = (request.AbsentEnrollmentIds ?? []).Distinct().ToList();
        var tardyEntries = (request.Tardies ?? [])
            .GroupBy(t => t.EnrollmentId)
            .Select(g => g.Last())
            .ToList();

        var roster = await LoadRosterAsync(request.ClassroomId, yearId);
        var session = slot.Session;

        var existingAbsences = session is null
            ? []
            : await db.AttendanceAbsences.Where(a => a.AttendanceSessionId == session.AttendanceSessionId).ToListAsync();

        var allowed = roster.Where(r => r.Active).Select(r => r.EnrollmentId)
            .Concat(existingAbsences.Select(a => a.EnrollmentId))
            .ToHashSet();

        if (absentIds.Any(id => !allowed.Contains(id)) || tardyEntries.Any(t => !allowed.Contains(t.EnrollmentId)))
            throw new InvalidOperationException("Hay estudiantes que no pertenecen a este curso.");

        if (tardyEntries.Any(t => absentIds.Contains(t.EnrollmentId)))
            throw new InvalidOperationException("No se puede registrar tardanza a un estudiante ausente.");

        if (isClass && tardyEntries.Count > 0)
        {
            if (await IsFirstOfDayAsync(request.ClassroomId, yearId, date, slot.Start!.Value))
                throw new InvalidOperationException("En la primera clase del día la llegada tarde la registra portería.");

            if (tardyEntries.Any(t => string.IsNullOrWhiteSpace(t.Observation)))
                throw new InvalidOperationException("La sanción pedagógica es obligatoria.");
        }

        if (session is null)
        {
            session = new AttendanceSession
            {
                YearId = yearId,
                ClassroomId = request.ClassroomId,
                Date = date,
                Kind = slot.Kind,
                AssignmentId = slot.AssignmentId,
                SubjectId = slot.SubjectId,
                StartTime = slot.Start,
                EndTime = slot.End,
                TakenById = access.UserId,
            };
            db.AttendanceSessions.Add(session);
        }
        else
        {
            session.UpdatedById = access.UserId;
            session.UpdatedAt = DateTime.UtcNow;
        }

        db.AttendanceAbsences.RemoveRange(existingAbsences.Where(a => !absentIds.Contains(a.EnrollmentId)));

        var absences = existingAbsences.Where(a => absentIds.Contains(a.EnrollmentId)).ToList();
        var newAbsences = new List<AttendanceAbsence>();

        foreach (var enrollmentId in absentIds.Where(id => absences.All(a => a.EnrollmentId != id)))
        {
            var absence = new AttendanceAbsence
            {
                Session = session,
                EnrollmentId = enrollmentId,
                Status = AbsenceStatuses.Absent,
            };
            db.AttendanceAbsences.Add(absence);
            absences.Add(absence);
            newAbsences.Add(absence);
        }

        await excuses.LinkCoveringAsync(session, newAbsences);

        if (isClass)
            await ApplyEvasionAsync(classroom, session, absences, slot.SubjectName ?? string.Empty, roster);

        var tardyCount = isClass
            ? await ApplyClassTardiesAsync(classroom, session, tardyEntries, slot.SubjectName ?? string.Empty, roster, access.UserId)
            : tardyEntries.Count;

        await db.SaveChangesAsync();

        if (!isClass)
            await ApplyDailyLateArrivalsAsync(request.ClassroomId, date, tardyEntries, roster, access.UserId, today);

        return new SaveSheetResult(
            session.AttendanceSessionId,
            absences.Count(a => a.Status == AbsenceStatuses.Absent),
            absences.Count(a => a.Status == AbsenceStatuses.Evasion),
            tardyCount);
    }

    private async Task ApplyEvasionAsync(
        ClassroomInfo classroom,
        AttendanceSession session,
        List<AttendanceAbsence> currentAbsences,
        string currentSubject,
        List<RosterEntry> roster)
    {
        var others = await db.AttendanceSessions
            .Where(s => s.ClassroomId == session.ClassroomId && s.Date == session.Date &&
                        s.Kind == AttendanceSessionKinds.Class && s.AttendanceSessionId != session.AttendanceSessionId)
            .Select(s => new { s.AttendanceSessionId, Start = s.StartTime!.Value, SubjectName = s.Subject!.Name })
            .ToListAsync();

        var otherIds = others.Select(o => o.AttendanceSessionId).ToList();
        var otherAbsences = await db.AttendanceAbsences
            .Where(a => otherIds.Contains(a.AttendanceSessionId))
            .ToListAsync();

        var day = others
            .Select(o => (
                o.Start,
                o.SubjectName,
                Absences: otherAbsences.Where(a => a.AttendanceSessionId == o.AttendanceSessionId).ToList()))
            .Append((Start: session.StartTime!.Value, SubjectName: currentSubject, Absences: currentAbsences))
            .ToList();

        foreach (var (start, subjectName, absences) in day)
        {
            foreach (var absence in absences)
            {
                var taken = day
                    .Where(other => other.Start != start)
                    .Select(other => (other.Start, Absent: other.Absences.Any(a => a.EnrollmentId == absence.EnrollmentId)));

                var status = AttendanceRules.IsEvasion(start, taken) ? AbsenceStatuses.Evasion : AbsenceStatuses.Absent;
                var becameEvasion = status == AbsenceStatuses.Evasion && absence.Status != AbsenceStatuses.Evasion;

                absence.Status = status;

                if (!becameEvasion)
                    continue;

                var student = roster.FirstOrDefault(r => r.EnrollmentId == absence.EnrollmentId);
                if (student is null)
                    continue;

                await notifications.QueueAsync(
                    NotificationCodes.AttEvasion,
                    new Dictionary<string, string>
                    {
                        ["estudiante"] = $"{student.FirstName} {student.LastName}",
                        ["curso"] = classroom.Name,
                        ["materia"] = subjectName,
                        ["hora"] = start.ToString("H:mm"),
                    },
                    new NotificationTarget(
                        StudentId: student.StudentId,
                        YearId: session.YearId,
                        CampusId: classroom.CampusId,
                        DedupKey: $"EVASION:{absence.EnrollmentId}:{session.Date:yyyyMMdd}:{start:HHmm}"));
            }
        }
    }

    private async Task<int> ApplyClassTardiesAsync(
        ClassroomInfo classroom,
        AttendanceSession session,
        List<TardyEntry> entries,
        string subjectName,
        List<RosterEntry> roster,
        int userId)
    {
        var existing = session.AttendanceSessionId == 0
            ? []
            : await db.ClassTardinesses.Where(t => t.AttendanceSessionId == session.AttendanceSessionId).ToListAsync();

        db.ClassTardinesses.RemoveRange(existing.Where(t => entries.All(e => e.EnrollmentId != t.EnrollmentId)));

        foreach (var entry in entries)
        {
            var observation = entry.Observation!.Trim();
            if (observation.Length > ObservationLength)
                observation = observation[..ObservationLength];

            var current = existing.FirstOrDefault(t => t.EnrollmentId == entry.EnrollmentId);
            if (current is not null)
            {
                current.Observation = observation;
                continue;
            }

            db.ClassTardinesses.Add(new ClassTardiness
            {
                Session = session,
                EnrollmentId = entry.EnrollmentId,
                Observation = observation,
                RecordedById = userId,
            });

            var student = roster.FirstOrDefault(r => r.EnrollmentId == entry.EnrollmentId);
            if (student is null)
                continue;

            await notifications.QueueAsync(
                NotificationCodes.AttClassTardy,
                new Dictionary<string, string>
                {
                    ["estudiante"] = $"{student.FirstName} {student.LastName}",
                    ["curso"] = classroom.Name,
                    ["materia"] = subjectName,
                    ["observacion"] = observation,
                },
                new NotificationTarget(
                    StudentId: student.StudentId,
                    YearId: session.YearId,
                    CampusId: classroom.CampusId,
                    DedupKey: $"TARDY:{entry.EnrollmentId}:{session.Date:yyyyMMdd}:{session.StartTime:HHmm}"));
        }

        return entries.Count;
    }

    private async Task ApplyDailyLateArrivalsAsync(
        int classroomId,
        DateOnly date,
        List<TardyEntry> entries,
        List<RosterEntry> roster,
        int userId,
        DateOnly today)
    {
        var rosterIds = roster.Select(r => r.EnrollmentId).ToList();
        var existing = await db.LateArrivals
            .Where(l => l.Date == date && rosterIds.Contains(l.EnrollmentId))
            .Select(l => new { l.LateArrivalId, l.EnrollmentId })
            .ToListAsync();

        foreach (var arrival in existing.Where(l => entries.All(e => e.EnrollmentId != l.EnrollmentId)))
            await lateArrivals.DeleteAsync(arrival.LateArrivalId);

        foreach (var entry in entries.Where(e => existing.All(l => l.EnrollmentId != e.EnrollmentId)))
            await lateArrivals.RegisterAsync(entry.EnrollmentId, date, userId, today);
    }

    private async Task<Slot> ResolveSlotAsync(int userId, ClassroomInfo classroom, short yearId, DateOnly date, TimeOnly? startTime)
    {
        var dailyLevels = (await settings.GetAsync()).DailyLevels;

        if (AttendanceRules.IsDailyLevel(classroom.Level, dailyLevels))
        {
            var dailySession = await db.AttendanceSessions.FirstOrDefaultAsync(s =>
                s.ClassroomId == classroom.ClassroomId && s.Date == date && s.Kind == AttendanceSessionKinds.Daily);

            var teaches = classroom.DirectorTeacherId == userId ||
                          await db.AcademicAssignments.AnyAsync(a =>
                              a.ClassroomId == classroom.ClassroomId && a.YearId == yearId && a.Active && a.TeacherId == userId);

            return new Slot(AttendanceSessionKinds.Daily, null, null, null, null, null, teaches, dailySession);
        }

        if (startTime is null)
            throw new InvalidOperationException(NoClassMessage);

        var session = await db.AttendanceSessions
            .Include(s => s.Subject)
            .Include(s => s.AcademicAssignment)
            .FirstOrDefaultAsync(s =>
                s.ClassroomId == classroom.ClassroomId && s.Date == date &&
                s.Kind == AttendanceSessionKinds.Class && s.StartTime == startTime);

        if (session is not null)
        {
            return new Slot(
                AttendanceSessionKinds.Class,
                session.AssignmentId,
                session.SubjectId,
                session.Subject?.Name,
                session.StartTime,
                session.EndTime,
                session.AcademicAssignment?.TeacherId == userId,
                session);
        }

        var weekday = AttendanceRules.WeekdayOf(date);
        var block = await db.Schedules
            .AsNoTracking()
            .Where(s => s.YearId == yearId && s.Type == ScheduleTypes.Class && s.Weekday == weekday &&
                        s.ClassroomId == classroom.ClassroomId && s.StartTime == startTime)
            .Select(s => new
            {
                s.AssignmentId,
                s.AcademicAssignment!.SubjectId,
                SubjectName = s.AcademicAssignment.Subject.Name,
                s.StartTime,
                s.EndTime,
                s.TeacherId,
            })
            .FirstOrDefaultAsync();

        if (block is null)
            throw new InvalidOperationException(NoClassMessage);

        return new Slot(
            AttendanceSessionKinds.Class,
            block.AssignmentId,
            block.SubjectId,
            block.SubjectName,
            block.StartTime,
            block.EndTime,
            block.TeacherId == userId,
            null);
    }

    private async Task<bool> IsFirstOfDayAsync(int classroomId, short yearId, DateOnly date, TimeOnly start)
    {
        var weekday = AttendanceRules.WeekdayOf(date);

        var blockStarts = await db.Schedules
            .Where(s => s.YearId == yearId && s.Type == ScheduleTypes.Class &&
                        s.Weekday == weekday && s.ClassroomId == classroomId)
            .Select(s => s.StartTime)
            .ToListAsync();

        var sessionStarts = await db.AttendanceSessions
            .Where(s => s.ClassroomId == classroomId && s.Date == date && s.Kind == AttendanceSessionKinds.Class)
            .Select(s => s.StartTime!.Value)
            .ToListAsync();

        return AttendanceRules.IsFirstClassOfDay(start, blockStarts.Concat(sessionStarts));
    }

    private async Task<List<ClassroomInfo>> DailyClassroomsAsync(
        int userId,
        int? classroomId,
        short yearId,
        byte weekday,
        List<string> dailyLevels)
    {
        var candidates = await db.Classrooms
            .AsNoTracking()
            .Where(c => classroomId != null
                ? c.ClassroomId == classroomId
                : c.DirectorTeacherId == userId ||
                  db.AcademicAssignments.Any(a =>
                      a.ClassroomId == c.ClassroomId && a.YearId == yearId && a.Active && a.TeacherId == userId))
            .Where(c => db.Schedules.Any(s =>
                s.YearId == yearId && s.Type == ScheduleTypes.Class && s.Weekday == weekday && s.ClassroomId == c.ClassroomId))
            .Select(c => new ClassroomInfo(c.ClassroomId, c.Name, c.CampusId, c.Grade.Level, c.DirectorTeacherId))
            .ToListAsync();

        return candidates.Where(c => AttendanceRules.IsDailyLevel(c.Level, dailyLevels)).ToList();
    }

    private async Task<ClassroomInfo> LoadClassroomAsync(int classroomId) =>
        await db.Classrooms
            .AsNoTracking()
            .Where(c => c.ClassroomId == classroomId)
            .Select(c => new ClassroomInfo(c.ClassroomId, c.Name, c.CampusId, c.Grade.Level, c.DirectorTeacherId))
            .FirstOrDefaultAsync()
        ?? throw new InvalidOperationException("El curso no existe.");

    private Task<List<RosterEntry>> LoadRosterAsync(int classroomId, short yearId) =>
        db.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassroomId == classroomId && e.YearId == yearId)
            .OrderBy(e => e.Student.User.LastName)
            .ThenBy(e => e.Student.User.FirstName)
            .Select(e => new RosterEntry(
                e.EnrollmentId,
                e.StudentId,
                e.Student.User.FirstName,
                e.Student.User.LastName,
                e.Status == EnrollmentStatuses.Active))
            .ToListAsync();
}
