using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AttendanceRollServiceTests
{
    private sealed class Scene
    {
        public required TestSchool School { get; init; }
        public required TestServices Services { get; init; }
        public RollAccess Admin => new(School.Marta.Id, false, true);
        public RollAccess T1 => new(School.T1.Id, true, false);
        public RollAccess T2 => new(School.T2.Id, true, false);
        public DateOnly Tuesday => School.Tuesday;

        public Task<SaveSheetResult> SaveAsync(TimeOnly start, params Enrollment[] absent) =>
            Services.Roll.SaveSheetAsync(Admin, Request(start, absent), Tuesday);

        public SaveSheetRequest Request(TimeOnly start, params Enrollment[] absent) =>
            new(School.C1001.ClassroomId, Tuesday, start, absent.Select(e => e.EnrollmentId).ToList(), []);

        public SaveSheetRequest Tardy(TimeOnly start, Enrollment student, string? observation, params Enrollment[] absent) =>
            new(School.C1001.ClassroomId, Tuesday, start, absent.Select(e => e.EnrollmentId).ToList(),
                [new TardyEntry(student.EnrollmentId, observation)]);

        public Task<string> StatusAsync(TimeOnly start, Enrollment student) =>
            School.Db.AttendanceAbsences
                .Where(a => a.Session.StartTime == start && a.EnrollmentId == student.EnrollmentId)
                .Select(a => a.Status)
                .SingleAsync();

        public Task<int> NotificationsAsync(string code) =>
            School.Db.Notifications.CountAsync(n => n.NotificationType.Code == code);
    }

    private static async Task<Scene> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        return new Scene { School = school, Services = TestServices.Create(db) };
    }

    [Fact]
    public async Task Absent_after_attending_an_earlier_class_is_evasion()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.ChemistryStart);

        var result = await scene.SaveAsync(TestSchool.PhysicsStart, scene.School.Beto);

        Assert.Equal(1, result.Evasions);
        Assert.Equal(0, result.Absences);
        Assert.Equal(AbsenceStatuses.Evasion, await scene.StatusAsync(TestSchool.PhysicsStart, scene.School.Beto));

        var notification = await scene.School.Db.Notifications.SingleAsync();
        Assert.Equal("Beto Paz (1001) no asistió a Física a las 9:00.", notification.Message);
        Assert.Equal(scene.School.Beto.StudentId, notification.StudentId);
    }

    [Fact]
    public async Task Absent_in_the_first_class_is_never_evasion()
    {
        var scene = await CreateAsync();

        await scene.SaveAsync(TestSchool.MathStart, scene.School.Beto);

        Assert.Equal(AbsenceStatuses.Absent, await scene.StatusAsync(TestSchool.MathStart, scene.School.Beto));
        Assert.Equal(0, await scene.School.Db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Absent_all_day_is_absence_not_evasion()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.ChemistryStart, scene.School.Beto);

        await scene.SaveAsync(TestSchool.PhysicsStart, scene.School.Beto);

        Assert.Equal(AbsenceStatuses.Absent, await scene.StatusAsync(TestSchool.ChemistryStart, scene.School.Beto));
        Assert.Equal(AbsenceStatuses.Absent, await scene.StatusAsync(TestSchool.PhysicsStart, scene.School.Beto));
    }

    [Fact]
    public async Task Saving_lists_out_of_order_still_detects_evasion()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.PhysicsStart, scene.School.Beto);
        Assert.Equal(AbsenceStatuses.Absent, await scene.StatusAsync(TestSchool.PhysicsStart, scene.School.Beto));

        await scene.SaveAsync(TestSchool.ChemistryStart);

        Assert.Equal(AbsenceStatuses.Evasion, await scene.StatusAsync(TestSchool.PhysicsStart, scene.School.Beto));
        Assert.Equal(1, await scene.NotificationsAsync(NotificationCodes.AttEvasion));
    }

    [Fact]
    public async Task Correcting_the_earlier_list_removes_the_evasion()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.ChemistryStart);
        await scene.SaveAsync(TestSchool.PhysicsStart, scene.School.Beto);

        await scene.SaveAsync(TestSchool.ChemistryStart, scene.School.Beto);

        Assert.Equal(AbsenceStatuses.Absent, await scene.StatusAsync(TestSchool.PhysicsStart, scene.School.Beto));
        Assert.Equal(1, await scene.NotificationsAsync(NotificationCodes.AttEvasion));
    }

    [Fact]
    public async Task Saving_twice_updates_the_same_session()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.PhysicsStart, scene.School.Beto);

        await scene.SaveAsync(TestSchool.PhysicsStart);

        var session = await scene.School.Db.AttendanceSessions.SingleAsync();
        Assert.Equal(0, await scene.School.Db.AttendanceAbsences.CountAsync());
        Assert.Equal(scene.School.Marta.Id, session.UpdatedById);
    }

    [Fact]
    public async Task The_list_survives_a_deleted_schedule_block()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.PhysicsStart, scene.School.Beto);
        scene.School.Db.Schedules.Remove(scene.School.PhysicsBlock);
        await scene.School.Db.SaveChangesAsync();

        var sheet = await scene.Services.Roll.GetSheetAsync(
            scene.Admin, scene.School.C1001.ClassroomId, scene.Tuesday, TestSchool.PhysicsStart, scene.Tuesday);
        var agenda = await scene.Services.Roll.GetAgendaAsync(scene.Admin, scene.Tuesday, scene.School.C1001.ClassroomId);

        Assert.Equal("Física", sheet.SubjectName);
        Assert.Equal(AbsenceStatuses.Absent, sheet.Students.Single(s => s.EnrollmentId == scene.School.Beto.EnrollmentId).Status);
        Assert.Contains(agenda, item => item.StartTime == TestSchool.PhysicsStart && item.Taken);
    }

    [Fact]
    public async Task A_missing_class_is_reported()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.SaveAsync(new TimeOnly(13, 0)));

        Assert.Equal("No hay una clase en ese horario.", error.Message);
    }

    [Fact]
    public async Task Tardy_needs_an_observation()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Roll.SaveSheetAsync(scene.Admin, scene.Tardy(TestSchool.ChemistryStart, scene.School.Ana, " "), scene.Tuesday));

        Assert.Equal("La sanción pedagógica es obligatoria.", error.Message);
    }

    [Fact]
    public async Task Tardy_is_not_allowed_in_the_first_class()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Roll.SaveSheetAsync(scene.Admin, scene.Tardy(TestSchool.MathStart, scene.School.Ana, "Expone"), scene.Tuesday));

        Assert.Equal("En la primera clase del día la llegada tarde la registra portería.", error.Message);
    }

    [Fact]
    public async Task A_class_tardy_is_saved_and_notified_once()
    {
        var scene = await CreateAsync();
        var request = scene.Tardy(TestSchool.ChemistryStart, scene.School.Ana, "Expone el tema");

        var result = await scene.Services.Roll.SaveSheetAsync(scene.Admin, request, scene.Tuesday);
        await scene.Services.Roll.SaveSheetAsync(scene.Admin, request, scene.Tuesday);

        var notification = await scene.School.Db.Notifications.SingleAsync();
        Assert.Equal(1, result.Tardies);
        Assert.Equal(1, await scene.School.Db.ClassTardinesses.CountAsync());
        Assert.Equal("Ana Ruiz (1001) llegó tarde a Química. Sanción pedagógica: Expone el tema", notification.Message);
    }

    [Fact]
    public async Task Tardy_for_an_absent_student_is_rejected()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Roll.SaveSheetAsync(
                scene.Admin,
                scene.Tardy(TestSchool.ChemistryStart, scene.School.Ana, "Expone", scene.School.Ana),
                scene.Tuesday));

        Assert.Equal("No se puede registrar tardanza a un estudiante ausente.", error.Message);
    }

    [Fact]
    public async Task A_removed_tardy_is_deleted()
    {
        var scene = await CreateAsync();
        await scene.Services.Roll.SaveSheetAsync(
            scene.Admin, scene.Tardy(TestSchool.ChemistryStart, scene.School.Ana, "Expone"), scene.Tuesday);

        await scene.SaveAsync(TestSchool.ChemistryStart);

        Assert.Equal(0, await scene.School.Db.ClassTardinesses.CountAsync());
    }

    [Fact]
    public async Task A_teacher_saves_their_own_class_today()
    {
        var scene = await CreateAsync();

        var result = await scene.Services.Roll.SaveSheetAsync(
            scene.T1, scene.Request(TestSchool.PhysicsStart, scene.School.Beto), scene.Tuesday);

        Assert.Equal(1, result.Absences);
    }

    [Fact]
    public async Task A_teacher_cannot_save_another_teachers_class()
    {
        var scene = await CreateAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scene.Services.Roll.SaveSheetAsync(scene.T2, scene.Request(TestSchool.PhysicsStart), scene.Tuesday));
    }

    [Fact]
    public async Task A_teacher_can_only_save_today()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Roll.SaveSheetAsync(scene.T1, scene.Request(TestSchool.PhysicsStart), scene.Tuesday.AddDays(1)));

        Assert.Equal("Solo puedes tomar o cambiar la lista del día de hoy.", error.Message);
    }

    [Fact]
    public async Task A_future_date_is_rejected()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Roll.SaveSheetAsync(scene.Admin, scene.Request(TestSchool.PhysicsStart), scene.Tuesday.AddDays(-1)));

        Assert.Equal("No se puede tomar lista de una fecha futura.", error.Message);
    }

    [Fact]
    public async Task A_date_outside_every_period_is_rejected()
    {
        var scene = await CreateAsync();
        var december = new DateOnly(2026, 12, 22);
        var request = new SaveSheetRequest(scene.School.C1001.ClassroomId, december, TestSchool.PhysicsStart, [], []);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Roll.SaveSheetAsync(scene.Admin, request, december));

        Assert.Equal("La fecha no pertenece a ningún periodo académico.", error.Message);
    }

    [Fact]
    public async Task A_stranger_enrollment_is_rejected()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.SaveAsync(TestSchool.PhysicsStart, scene.School.Dani));

        Assert.Equal("Hay estudiantes que no pertenecen a este curso.", error.Message);
    }

    [Fact]
    public async Task The_sheet_tells_who_attended_an_earlier_class()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.ChemistryStart, scene.School.Caro);

        var sheet = await scene.Services.Roll.GetSheetAsync(
            scene.T1, scene.School.C1001.ClassroomId, scene.Tuesday, TestSchool.PhysicsStart, scene.Tuesday);

        Assert.Equal(new[] { "Lima, Caro", "Paz, Beto", "Ruiz, Ana" }, sheet.Students.Select(s => s.Name));
        Assert.Equal("Química a las 7:25", sheet.Students.Single(s => s.EnrollmentId == scene.School.Beto.EnrollmentId).EarlierClass);
        Assert.Null(sheet.Students.Single(s => s.EnrollmentId == scene.School.Caro.EnrollmentId).EarlierClass);
        Assert.False(sheet.FirstOfDay);
        Assert.True(sheet.CanEdit);
        Assert.Null(sheet.SessionId);
    }

    [Fact]
    public async Task The_teacher_agenda_lists_their_classes()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(TestSchool.PhysicsStart);

        var agenda = await scene.Services.Roll.GetAgendaAsync(scene.T2, scene.Tuesday, null);

        Assert.Equal(new TimeOnly?[] { TestSchool.MathStart, TestSchool.ChemistryStart }, agenda.Select(a => a.StartTime));
        Assert.All(agenda, item => Assert.False(item.Taken));
    }

    [Fact]
    public async Task A_daily_classroom_has_one_entry_and_no_evasion()
    {
        var scene = await CreateAsync();

        var agenda = await scene.Services.Roll.GetAgendaAsync(scene.T1, scene.Tuesday, null);
        var daily = agenda.Single(item => item.ClassroomId == scene.School.C3A.ClassroomId);

        Assert.Equal(AttendanceSessionKinds.Daily, daily.Kind);
        Assert.Null(daily.StartTime);

        await scene.Services.Roll.SaveSheetAsync(
            scene.T1,
            new SaveSheetRequest(scene.School.C3A.ClassroomId, scene.Tuesday, null, [scene.School.Dani.EnrollmentId], []),
            scene.Tuesday);

        var absence = await scene.School.Db.AttendanceAbsences.Include(a => a.Session).SingleAsync();
        Assert.Equal(AbsenceStatuses.Absent, absence.Status);
        Assert.Equal(AttendanceSessionKinds.Daily, absence.Session.Kind);
    }

    [Fact]
    public async Task A_daily_tardy_becomes_a_late_arrival()
    {
        var scene = await CreateAsync();
        var request = new SaveSheetRequest(
            scene.School.C3A.ClassroomId, scene.Tuesday, null, [], [new TardyEntry(scene.School.Dani.EnrollmentId, null)]);

        await scene.Services.Roll.SaveSheetAsync(scene.T1, request, scene.Tuesday);

        var sheet = await scene.Services.Roll.GetSheetAsync(
            scene.T1, scene.School.C3A.ClassroomId, scene.Tuesday, null, scene.Tuesday);

        Assert.Equal(1, await scene.School.Db.LateArrivals.CountAsync(l => l.EnrollmentId == scene.School.Dani.EnrollmentId));
        Assert.Equal(0, await scene.School.Db.ClassTardinesses.CountAsync());
        Assert.True(sheet.Students.Single().Late);

        await scene.Services.Roll.SaveSheetAsync(
            scene.T1, new SaveSheetRequest(scene.School.C3A.ClassroomId, scene.Tuesday, null, [], []), scene.Tuesday);

        Assert.Equal(0, await scene.School.Db.LateArrivals.CountAsync());
    }

    [Fact]
    public async Task Sunday_has_an_empty_agenda()
    {
        var scene = await CreateAsync();

        Assert.Empty(await scene.Services.Roll.GetAgendaAsync(scene.T1, new DateOnly(2026, 11, 15), null));
    }
}
