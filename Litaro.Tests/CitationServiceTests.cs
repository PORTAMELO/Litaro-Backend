using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class CitationServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateOnly NextTuesday = new(2026, 11, 17);
    private static readonly DateOnly NextWednesday = new(2026, 11, 18);

    private sealed record Context(
        TestSchool School,
        ObservationService Observations,
        CitationService Citations,
        NotificationService Notifications,
        User Mother,
        User Father,
        Schedule Slot);

    private static async Task<Context> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        var coord = await db.Roles.SingleAsync(r => r.Name == "Coord");

        TestData.AddNotificationType(db, NotificationCodes.CitationParent,
            "Tiene una citación el {fecha} a las {hora} con {responsable} para hablar sobre {estudiante}. Motivo: {motivo}", coord);
        TestData.AddNotificationType(db, NotificationCodes.CitationCoordination,
            "{estudiante} ({curso}) tiene una observación de convivencia Tipo {tipo} del {fecha}. Agende la citación al acudiente.", coord);

        var mother = TestData.AddUser(db, "Luisa", "Ruiz");
        var father = TestData.AddUser(db, "Pedro", "Ruiz");
        db.Parents.AddRange(
            new Parent { ParentId = mother.Id, Relationship = "Madre" },
            new Parent { ParentId = father.Id, Relationship = "Padre" });
        db.ParentStudents.AddRange(
            new ParentStudent { ParentId = father.Id, StudentId = school.Ana.StudentId, PrimaryContact = false },
            new ParentStudent { ParentId = mother.Id, StudentId = school.Ana.StudentId, PrimaryContact = true });

        var slot = new Schedule
        {
            YearId = TestSchool.YearId,
            CampusId = school.Campus.CampusId,
            Type = ScheduleTypes.ParentAttention,
            Weekday = 2,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            TeacherId = school.T2.Id,
        };
        db.Schedules.Add(slot);
        await db.SaveChangesAsync();

        var notifications = new NotificationService(db);
        var citations = new CitationService(db, notifications);
        var observations = new ObservationService(db, new DocumentService(db, new FakeDocumentStorage()), citations, new ObservationConsolidatedService(db, new ObservationSettingsService(db), notifications));

        return new Context(school, observations, citations, notifications, mother, father, slot);
    }

    private static ObservationAccess Teacher(User user) => new(user.Id, false);

    private static ObservationAccess Coordination(TestSchool school) => new(school.Marta.Id, true);

    private static Task<int> ObserveAsync(Context ctx, User teacher, Enrollment enrollment, string type) =>
        ctx.Observations.CreateAsync(
            Teacher(teacher),
            new SaveObservationRequest(enrollment.EnrollmentId, Today, "No respeta el turno.", false, true, type, null),
            Today);

    [Fact]
    public async Task Type_I_observation_creates_a_citation_to_the_primary_parent_for_the_teacher()
    {
        var ctx = await CreateAsync();

        var observationId = await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);

        var citation = await ctx.School.Db.Citations.SingleAsync();
        Assert.Equal(observationId, citation.ObservationId);
        Assert.Equal(ctx.Mother.Id, citation.ParentId);
        Assert.Equal(ctx.School.T2.Id, citation.ResponsibleUserId);
        Assert.Equal(CitationOrigins.Automatic, citation.Origin);
        Assert.Equal(CitationStatuses.ToSchedule, citation.Status);
        Assert.Equal("Observación Convivencia Tipo I del 10/11/2026.", citation.Reason);
        Assert.Empty(ctx.School.Db.Notifications);
    }

    [Fact]
    public async Task Type_II_observation_goes_to_coordination_and_alerts_it()
    {
        var ctx = await CreateAsync();

        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeII);

        var citation = await ctx.School.Db.Citations.SingleAsync();
        Assert.Null(citation.ResponsibleUserId);

        var alert = Assert.Single(await ctx.Notifications.ListAsync(ctx.School.Marta.Id, null, null));
        Assert.Equal("Ana Ruiz (1001) tiene una observación de convivencia Tipo II del 10/11/2026. Agende la citación al acudiente.", alert.Message);

        var listed = Assert.Single(await ctx.Citations.ListAsync(Coordination(ctx.School), new CitationFilter(), Today));
        Assert.Equal(CitationService.CoordinationName, listed.ResponsibleName);
    }

    [Fact]
    public async Task Scheduling_sets_the_chosen_parent_or_fills_the_primary_one()
    {
        var ctx = await CreateAsync();
        var coordination = Coordination(ctx.School);
        var withoutParent = new Citation
        {
            StudentId = ctx.School.Ana.StudentId,
            Origin = CitationOrigins.Manual,
            Reason = "Seguimiento.",
            CreatedById = ctx.School.Marta.Id,
        };
        ctx.School.Db.Citations.Add(withoutParent);
        await ctx.School.Db.SaveChangesAsync();

        await ctx.Citations.ScheduleAsync(coordination, withoutParent.CitationId, new ScheduleCitationRequest(NextWednesday, new TimeOnly(9, 0), null), Today);
        Assert.Equal(ctx.Mother.Id, withoutParent.ParentId);

        var id = await ctx.Citations.CreateManualAsync(coordination, new CreateCitationRequest(ctx.School.Ana.StudentId, null, "Rendimiento."));
        await ctx.Citations.ScheduleAsync(coordination, id, new ScheduleCitationRequest(NextWednesday, new TimeOnly(10, 0), null, ctx.Father.Id), Today);
        Assert.Equal(ctx.Father.Id, (await ctx.School.Db.Citations.SingleAsync(c => c.CitationId == id)).ParentId);

        var other = await ctx.Citations.CreateManualAsync(coordination, new CreateCitationRequest(ctx.School.Ana.StudentId, null, "Otro."));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.ScheduleAsync(coordination, other, new ScheduleCitationRequest(NextWednesday, new TimeOnly(11, 0), null, ctx.School.T2.Id), Today));
        Assert.Equal("El acudiente no está asociado al estudiante.", error.Message);
    }

    [Fact]
    public async Task Teacher_with_attention_slots_must_schedule_inside_them()
    {
        var ctx = await CreateAsync();
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);
        var id = (await ctx.School.Db.Citations.SingleAsync()).CitationId;
        var teacher = Teacher(ctx.School.T2);

        var noSlot = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.ScheduleAsync(teacher, id, new ScheduleCitationRequest(NextTuesday, new TimeOnly(10, 0), null), Today));
        var wrongDay = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.ScheduleAsync(teacher, id, new ScheduleCitationRequest(NextWednesday, null, ctx.Slot.ScheduleId), Today));
        var wrongHour = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.ScheduleAsync(teacher, id, new ScheduleCitationRequest(NextTuesday, new TimeOnly(11, 0), ctx.Slot.ScheduleId), Today));

        Assert.Equal("Elija una de sus franjas de atención a padres.", noSlot.Message);
        Assert.Equal("La franja elegida es los martes; la fecha no coincide.", wrongDay.Message);
        Assert.Equal("La hora debe estar entre 10:00 y 11:00.", wrongHour.Message);

        await ctx.Citations.ScheduleAsync(teacher, id, new ScheduleCitationRequest(NextTuesday, null, ctx.Slot.ScheduleId), Today);

        var citation = await ctx.School.Db.Citations.SingleAsync();
        Assert.Equal(CitationStatuses.Scheduled, citation.Status);
        Assert.Equal(new TimeOnly(10, 0), citation.ScheduledTime);
    }

    [Fact]
    public async Task Parent_notification_is_seen_only_by_that_parent()
    {
        var ctx = await CreateAsync();
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);
        var id = (await ctx.School.Db.Citations.SingleAsync()).CitationId;

        await ctx.Citations.ScheduleAsync(Teacher(ctx.School.T2), id, new ScheduleCitationRequest(NextTuesday, null, ctx.Slot.ScheduleId), Today);

        var mother = Assert.Single(await ctx.Notifications.ListAsync(ctx.Mother.Id, null, null));
        Assert.Equal(
            "Tiene una citación el martes 17/11/2026 a las 10:00 con Tania Dos para hablar sobre Ana Ruiz. Motivo: Observación Convivencia Tipo I del 10/11/2026.",
            mother.Message);
        Assert.Empty(await ctx.Notifications.ListAsync(ctx.Father.Id, null, null));
        Assert.Empty(await ctx.Notifications.ListAsync(ctx.School.Marta.Id, null, null));
    }

    [Fact]
    public async Task Coordination_without_slots_uses_free_time_and_takes_the_citation()
    {
        var ctx = await CreateAsync();
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeIII);
        var id = (await ctx.School.Db.Citations.SingleAsync()).CitationId;
        var coordination = Coordination(ctx.School);

        var noTime = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.ScheduleAsync(coordination, id, new ScheduleCitationRequest(NextWednesday, null, null), Today));
        Assert.Equal("Indique la hora de la citación.", noTime.Message);

        await ctx.Citations.ScheduleAsync(coordination, id, new ScheduleCitationRequest(NextWednesday, new TimeOnly(14, 30), null), Today);

        var citation = await ctx.School.Db.Citations.SingleAsync();
        Assert.Equal(ctx.School.Marta.Id, citation.ResponsibleUserId);
        Assert.Equal(new TimeOnly(14, 30), citation.ScheduledTime);
    }

    [Fact]
    public async Task Teacher_cannot_manage_coordination_citations()
    {
        var ctx = await CreateAsync();
        await ObserveAsync(ctx, ctx.School.T1, ctx.School.Dani, CoexistenceTypes.TypeII);
        var id = (await ctx.School.Db.Citations.SingleAsync()).CitationId;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ctx.Citations.ScheduleAsync(Teacher(ctx.School.T2), id, new ScheduleCitationRequest(NextWednesday, new TimeOnly(9, 0), null), Today));
    }

    [Fact]
    public async Task Past_date_is_rejected()
    {
        var ctx = await CreateAsync();
        var id = await ctx.Citations.CreateManualAsync(Coordination(ctx.School), new CreateCitationRequest(ctx.School.Ana.StudentId, null, "Adaptación."));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.ScheduleAsync(Coordination(ctx.School), id, new ScheduleCitationRequest(Today.AddDays(-1), new TimeOnly(9, 0), null), Today));

        Assert.Equal("La fecha de la citación no puede ser pasada.", error.Message);
    }

    [Fact]
    public async Task Reschedule_keeps_the_original_attempt_and_links_the_new_one()
    {
        var ctx = await CreateAsync();
        var coordination = Coordination(ctx.School);
        var id = await ctx.Citations.CreateManualAsync(coordination, new CreateCitationRequest(ctx.School.Ana.StudentId, null, "Rendimiento."));
        await ctx.Citations.ScheduleAsync(coordination, id, new ScheduleCitationRequest(NextTuesday, new TimeOnly(8, 0), null), Today);

        var newId = await ctx.Citations.RescheduleAsync(coordination, id, new ScheduleCitationRequest(NextWednesday, new TimeOnly(9, 0), null), "No pudo asistir.", Today);

        var original = await ctx.School.Db.Citations.SingleAsync(c => c.CitationId == id);
        var next = await ctx.School.Db.Citations.SingleAsync(c => c.CitationId == newId);
        Assert.Equal(CitationStatuses.Rescheduled, original.Status);
        Assert.Equal("No pudo asistir.", original.Note);
        Assert.Equal(id, next.RescheduledFromId);
        Assert.Equal(CitationStatuses.Scheduled, next.Status);
        Assert.Equal(NextWednesday, next.ScheduledDate);
        Assert.Equal(2, (await ctx.Notifications.ListAsync(ctx.Mother.Id, null, null)).Count);
    }

    [Fact]
    public async Task Manual_citation_requires_reason_scope_and_a_linked_parent()
    {
        var ctx = await CreateAsync();
        var teacher = Teacher(ctx.School.T2);

        var noReason = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.CreateManualAsync(teacher, new CreateCitationRequest(ctx.School.Ana.StudentId, null, "  ")));
        var otherParent = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.CreateManualAsync(teacher, new CreateCitationRequest(ctx.School.Beto.StudentId, ctx.Mother.Id, "Hablar.")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ctx.Citations.CreateManualAsync(teacher, new CreateCitationRequest(ctx.School.Dani.StudentId, null, "Hablar.")));

        Assert.Equal("Escriba el motivo de la citación.", noReason.Message);
        Assert.Equal("El acudiente no está asociado al estudiante.", otherParent.Message);

        var id = await ctx.Citations.CreateManualAsync(teacher, new CreateCitationRequest(ctx.School.Ana.StudentId, ctx.Father.Id, "Adaptación al curso."));
        var citation = await ctx.School.Db.Citations.SingleAsync(c => c.CitationId == id);
        Assert.Equal(ctx.Father.Id, citation.ParentId);
        Assert.Equal(CitationOrigins.Manual, citation.Origin);
        Assert.Equal(ctx.School.T2.Id, citation.ResponsibleUserId);
    }

    [Fact]
    public async Task Flags_are_independent_but_attendance_needs_a_date()
    {
        var ctx = await CreateAsync();
        var coordination = Coordination(ctx.School);
        var id = await ctx.Citations.CreateManualAsync(coordination, new CreateCitationRequest(ctx.School.Ana.StudentId, null, "Rendimiento."));

        await ctx.Citations.SetFlagsAsync(coordination, id, new CitationFlagsRequest(false, true, false));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ctx.Citations.SetFlagsAsync(coordination, id, new CitationFlagsRequest(true, true, true)));

        var citation = await ctx.School.Db.Citations.SingleAsync();
        Assert.True(citation.ParentInformed);
        Assert.False(citation.StudentInformed);
        Assert.Equal("Agende la citación antes de marcar la asistencia.", error.Message);
    }

    [Fact]
    public async Task Consecutive_misses_count_back_until_the_last_attended_citation()
    {
        var ctx = await CreateAsync();
        var db = ctx.School.Db;

        Citation Past(int daysAgo, bool attended, string status = CitationStatuses.Closed) => new()
        {
            StudentId = ctx.School.Ana.StudentId,
            ParentId = ctx.Mother.Id,
            Reason = "Seguimiento.",
            ScheduledDate = Today.AddDays(-daysAgo),
            ScheduledTime = new TimeOnly(10, 0),
            Status = status,
            Attended = attended,
            CreatedById = ctx.School.Marta.Id,
        };

        db.Citations.AddRange(
            Past(30, false),
            Past(20, true),
            Past(10, false, CitationStatuses.Rescheduled),
            Past(5, false, CitationStatuses.Cancelled),
            Past(3, false));
        await db.SaveChangesAsync();

        var misses = await ctx.Citations.ConsecutiveMissesAsync([ctx.Mother.Id, ctx.Father.Id], Today);

        Assert.Equal(2, misses[ctx.Mother.Id]);
        Assert.False(misses.ContainsKey(ctx.Father.Id));
    }

    [Fact]
    public async Task Teacher_lists_only_their_citations_and_coordination_all()
    {
        var ctx = await CreateAsync();
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T1, ctx.School.Dani, CoexistenceTypes.TypeI);

        var t2 = await ctx.Citations.ListAsync(Teacher(ctx.School.T2), new CitationFilter(), Today);
        var all = await ctx.Citations.ListAsync(Coordination(ctx.School), new CitationFilter(), Today);

        Assert.Equal("Ruiz, Ana", Assert.Single(t2).StudentName);
        Assert.True(t2[0].CanManage);
        Assert.Equal(2, all.Count);
    }
}
