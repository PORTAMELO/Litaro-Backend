using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class StudyPlannerServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastDue = new(2026, 2, 6, 22, 0, 0, DateTimeKind.Utc);

    private sealed record Setup(TestSchool School, StudyPlannerService Service, GradingDeadlineService Deadlines, FakeDocumentStorage Storage);

    private static async Task<Setup> CreateAsync(DateTime? dueAt)
    {
        var school = await TestSchool.CreateAsync(TestDb.Create());
        var coord = await school.Db.Roles.SingleAsync(r => r.Name == "Coord");

        TestData.AddNotificationType(school.Db, NotificationCodes.PlannerOverdue,
            "{docente} no cargó el plan de estudio de: {asignaciones}. El plazo venció el {fecha}.", coord);
        TestData.AddNotificationType(school.Db, NotificationCodes.PlannerRequest,
            "{docente} solicita gestionar la carga del plan de estudio de {materia} · {curso}.", coord);

        var storage = new FakeDocumentStorage();
        var deadlines = new GradingDeadlineService(school.Db);
        var service = new StudyPlannerService(school.Db, new DocumentService(school.Db, storage), deadlines, new NotificationService(school.Db));

        if (dueAt is not null)
        {
            var current = await deadlines.GetAsync(TestSchool.YearId);
            await deadlines.SaveAsync(current with { PlannerDueAt = dueAt }, school.Marta.Id, Now);
        }

        return new Setup(school, service, deadlines, storage);
    }

    private static GradingAccess Access(User user, PermissionFlags planner = PermissionFlags.Create) =>
        new(user.Id, PermissionFlags.None, PermissionFlags.None, planner);

    private static Task<int> AssignmentAsync(TestSchool school, Classroom classroom, Subject subject) =>
        school.Db.AcademicAssignments
            .Where(a => a.ClassroomId == classroom.ClassroomId && a.SubjectId == subject.SubjectId)
            .Select(a => a.AssignmentId)
            .SingleAsync();

    [Fact]
    public async Task Teacher_uploads_own_before_deadline_and_replaces()
    {
        var setup = await CreateAsync(Now.AddDays(1));
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);

        await setup.Service.UploadAsync(Access(setup.School.T2), math, FakeDocumentStorage.Pdf("v1.pdf"), Now);
        await setup.Service.UploadAsync(Access(setup.School.T2), math, FakeDocumentStorage.Pdf("v2.pdf"), Now);

        var own = await setup.Service.ListOwnAsync(Access(setup.School.T2), Now);

        Assert.Equal(2, own.Rows.Count);
        Assert.False(own.Overdue);
        Assert.Equal("v2.pdf", own.Rows.Single(r => r.AssignmentId == math).Document!.FileName);
        Assert.Equal(1, await setup.School.Db.Documents.CountAsync(d => d.IsCurrent));
    }

    [Fact]
    public async Task Teacher_cannot_upload_after_deadline_but_update_can()
    {
        var setup = await CreateAsync(PastDue);
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.UploadAsync(Access(setup.School.T2), math, FakeDocumentStorage.Pdf(), Now));

        Assert.Equal("El plazo para cargar el plan de estudio venció el 06/02/2026 17:00.", error.Message);

        var saved = await setup.Service.UploadAsync(Access(setup.School.Marta, PermissionFlags.Full), math, FakeDocumentStorage.Pdf(), Now);

        Assert.Equal("Marta Rojas", saved.UploadedBy);
    }

    [Fact]
    public async Task Teacher_deletes_own_before_deadline_and_keeps_history()
    {
        var setup = await CreateAsync(Now.AddDays(1));
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);

        await setup.Service.UploadAsync(Access(setup.School.T2), math, FakeDocumentStorage.Pdf(), Now);
        await setup.Service.DeleteAsync(Access(setup.School.T2), math, Now);

        var own = await setup.Service.ListOwnAsync(Access(setup.School.T2), Now);
        var row = await setup.School.Db.Documents.SingleAsync();

        Assert.False(own.Rows.Single(r => r.AssignmentId == math).Delivered);
        Assert.False(row.IsCurrent);
        Assert.Equal(setup.School.T2.Id, row.ReplacedById);
        Assert.Single(setup.Storage.Files);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.DeleteAsync(Access(setup.School.T2), math, Now));

        Assert.Equal("La asignación no tiene plan de estudio.", error.Message);
    }

    [Fact]
    public async Task Teacher_cannot_delete_after_deadline_or_foreign_but_update_can()
    {
        var setup = await CreateAsync(PastDue);
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);
        var admin = Access(setup.School.Marta, PermissionFlags.Full);

        await setup.Service.UploadAsync(admin, math, FakeDocumentStorage.Pdf(), Now);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.DeleteAsync(Access(setup.School.T2), math, Now));

        Assert.Equal("El plazo para cargar el plan de estudio venció el 06/02/2026 17:00.", error.Message);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.DeleteAsync(Access(setup.School.T1), math, Now));

        await setup.Service.DeleteAsync(admin, math, Now);

        Assert.Equal(0, await setup.School.Db.Documents.CountAsync(d => d.IsCurrent));
    }

    [Fact]
    public async Task Teacher_cannot_upload_foreign_assignment()
    {
        var setup = await CreateAsync(Now.AddDays(1));
        var physics = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Physics);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            setup.Service.UploadAsync(Access(setup.School.T2), physics, FakeDocumentStorage.Pdf(), Now));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            setup.Service.OpenAsync(Access(setup.School.T2), false, physics));
    }

    [Fact]
    public async Task Open_returns_own_and_panel_documents()
    {
        var setup = await CreateAsync(Now.AddDays(1));
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);

        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.OpenAsync(Access(setup.School.T2), false, math));

        await setup.Service.UploadAsync(Access(setup.School.T2), math, FakeDocumentStorage.Pdf(), Now);

        var own = await setup.Service.OpenAsync(Access(setup.School.T2), false, math);
        var panel = await setup.Service.OpenAsync(Access(setup.School.Marta, PermissionFlags.None), true, math);

        Assert.Equal("plan.pdf", own.Document.FileName);
        Assert.Equal("plan.pdf", panel.Document.FileName);
    }

    [Fact]
    public async Task Request_only_after_deadline_and_once()
    {
        var before = await CreateAsync(Now.AddDays(1));
        var beforeMath = await AssignmentAsync(before.School, before.School.C1001, before.School.Math);

        await Assert.ThrowsAsync<InvalidOperationException>(() => before.Service.RequestAsync(Access(before.School.T2), beforeMath, Now));

        var setup = await CreateAsync(PastDue);
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);

        await setup.Service.RequestAsync(Access(setup.School.T2), math, Now);

        var notification = await setup.School.Db.Notifications.SingleAsync();

        Assert.Equal("Tania Dos solicita gestionar la carga del plan de estudio de Matemáticas · 1001.", notification.Message);
        Assert.Equal("AcademicAssignment", notification.SourceTable);
        Assert.Equal(math, notification.SourceId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.RequestAsync(Access(setup.School.T2), math, Now));

        Assert.Equal("La solicitud ya fue enviada.", error.Message);
        Assert.True((await setup.Service.ListOwnAsync(Access(setup.School.T2), Now)).Rows.Single(r => r.AssignmentId == math).RequestPending);
    }

    [Fact]
    public async Task Panel_lists_delivered_and_pending()
    {
        var setup = await CreateAsync(Now.AddDays(1));
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);

        await setup.Service.UploadAsync(Access(setup.School.T2), math, FakeDocumentStorage.Pdf(), Now);

        var panel = await setup.Service.PanelAsync(Access(setup.School.Marta, PermissionFlags.Read), Now);

        Assert.Equal(5, panel.Rows.Count);
        Assert.Single(panel.Rows, r => r.Delivered);
        Assert.Equal("Décimo", panel.Rows.Single(r => r.AssignmentId == math).GradeName);
        Assert.False(panel.CanUploadAny);
    }

    [Fact]
    public async Task Overdue_notifies_each_pending_teacher_once()
    {
        var setup = await CreateAsync(PastDue);
        var math = await AssignmentAsync(setup.School, setup.School.C1001, setup.School.Math);

        await setup.Service.UploadAsync(Access(setup.School.Marta, PermissionFlags.Full), math, FakeDocumentStorage.Pdf(), Now);

        Assert.Equal(2, await setup.Service.NotifyOverdueAsync(Now));
        Assert.Equal(0, await setup.Service.NotifyOverdueAsync(Now));

        var messages = await setup.School.Db.Notifications.Select(n => n.Message).ToListAsync();

        Assert.Contains("Tania Dos no cargó el plan de estudio de: Química · 1001. El plazo venció el 06/02/2026 17:00.", messages);
    }

    [Fact]
    public async Task Moving_the_deadline_rearms_the_overdue_alert()
    {
        var setup = await CreateAsync(PastDue);

        Assert.Equal(2, await setup.Service.NotifyOverdueAsync(Now));

        var current = await setup.Deadlines.GetAsync(TestSchool.YearId);
        await setup.Deadlines.SaveAsync(current with { PlannerDueAt = Now.AddDays(5) }, setup.School.Marta.Id, Now);

        Assert.Equal(0, await setup.Service.NotifyOverdueAsync(Now));
        Assert.Equal(2, await setup.Service.NotifyOverdueAsync(Now.AddDays(6)));
        Assert.Equal(0, await setup.Service.NotifyOverdueAsync(Now.AddDays(7)));
    }

    [Fact]
    public async Task Planners_use_the_active_year_that_contains_today()
    {
        var setup = await CreateAsync(PastDue);
        setup.School.Db.AcademicYears.Add(new AcademicYear
        {
            YearId = 2027,
            StartDate = new DateTime(2027, 1, 18, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 11, 26, 0, 0, 0, DateTimeKind.Utc),
            Active = true,
        });
        await setup.School.Db.SaveChangesAsync();

        var own = await setup.Service.ListOwnAsync(Access(setup.School.T2), Now);

        Assert.Equal(TestSchool.YearId, own.YearId);
        Assert.Equal(2, own.Rows.Count);
        Assert.Equal(2, await setup.Service.NotifyOverdueAsync(Now));
    }

    [Fact]
    public async Task Overdue_skips_inactive_teachers()
    {
        var setup = await CreateAsync(PastDue);
        var teacher = await setup.School.Db.Teachers.SingleAsync(t => t.TeacherId == setup.School.T1.Id);
        teacher.Active = false;
        await setup.School.Db.SaveChangesAsync();

        Assert.Equal(1, await setup.Service.NotifyOverdueAsync(Now));
    }

    [Fact]
    public async Task Overdue_does_nothing_without_or_before_deadline()
    {
        var none = await CreateAsync(null);
        var early = await CreateAsync(Now.AddDays(1));

        Assert.Equal(0, await none.Service.NotifyOverdueAsync(Now));
        Assert.Equal(0, await early.Service.NotifyOverdueAsync(Now));
        Assert.Equal(0, await early.School.Db.JobRuns.CountAsync());
    }
}
