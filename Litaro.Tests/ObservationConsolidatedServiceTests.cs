using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class ObservationConsolidatedServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private sealed record Context(
        TestSchool School,
        ObservationService Observations,
        ObservationConsolidatedService Consolidated,
        ObservationSettingsService Settings,
        NotificationService Notifications);

    private static async Task<Context> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        var coord = await db.Roles.SingleAsync(r => r.Name == "Coord");

        TestData.AddNotificationType(db, NotificationCodes.ObservationTypeIThreshold,
            "{estudiante} ({curso}) alcanzó {cantidad} situaciones Tipo I en {anio}.", coord);

        var notifications = new NotificationService(db);
        var settings = new ObservationSettingsService(db);
        var consolidated = new ObservationConsolidatedService(db, settings, notifications);
        var observations = new ObservationService(
            db,
            new DocumentService(db, new FakeDocumentStorage()),
            new CitationService(db, notifications),
            consolidated);

        return new Context(school, observations, consolidated, settings, notifications);
    }

    private static Task<int> ObserveAsync(
        Context ctx,
        User teacher,
        Enrollment enrollment,
        string? type,
        bool academic = false,
        DateOnly? date = null,
        int? subjectId = null) =>
        ctx.Observations.CreateAsync(
            new ObservationAccess(teacher.Id, false),
            new SaveObservationRequest(enrollment.EnrollmentId, date ?? Today, "Situación en clase.", academic, type is not null, type, subjectId),
            Today);

    private Task<List<NotificationDto>> AlertsAsync(Context ctx) =>
        ctx.Notifications.ListAsync(ctx.School.Marta.Id, null, null);

    [Fact]
    public async Task Coordination_is_alerted_once_when_the_type_I_threshold_is_reached()
    {
        var ctx = await CreateAsync();

        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T1, ctx.School.Ana, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeII);
        Assert.Empty(await AlertsAsync(ctx));

        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);

        var alerts = (await AlertsAsync(ctx))
            .Where(n => n.Message.Contains("situaciones Tipo I"))
            .ToList();
        Assert.Equal("Ana Ruiz (1001) alcanzó 3 situaciones Tipo I en 2026.", Assert.Single(alerts).Message);
    }

    [Fact]
    public async Task Threshold_is_configurable()
    {
        var ctx = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.Settings.SaveAsync(new ObservationSettings(0)));
        Assert.Equal("Las situaciones Tipo I para la alerta deben estar entre 1 y 50.", error.Message);
        Assert.Equal(3, (await ctx.Settings.GetAsync()).TypeIThreshold);

        await ctx.Settings.SaveAsync(new ObservationSettings(2));
        await ctx.Settings.SaveAsync(new ObservationSettings(2));
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Beto, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Beto, CoexistenceTypes.TypeI);

        Assert.Equal(2, (await ctx.Settings.GetAsync()).TypeIThreshold);
        Assert.Single(await AlertsAsync(ctx));
    }

    [Fact]
    public async Task Changing_an_observation_to_type_I_also_counts()
    {
        var ctx = await CreateAsync();
        await ctx.Settings.SaveAsync(new ObservationSettings(1));
        var id = await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, null, academic: true);
        Assert.Empty(await AlertsAsync(ctx));

        await ctx.Observations.UpdateAsync(
            new ObservationAccess(ctx.School.T2.Id, false),
            id,
            new SaveObservationRequest(ctx.School.Ana.EnrollmentId, Today, "Situación en clase.", true, true, CoexistenceTypes.TypeI, null),
            Today);

        Assert.Single(await AlertsAsync(ctx));
    }

    [Fact]
    public async Task Ranking_orders_students_by_type_I_and_flags_the_threshold()
    {
        var ctx = await CreateAsync();
        for (var i = 0; i < 3; i++)
            await ObserveAsync(ctx, ctx.School.T2, ctx.School.Beto, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeIII);
        await ObserveAsync(ctx, ctx.School.T1, ctx.School.Dani, null, academic: true);

        var ranking = await ctx.Consolidated.RankingAsync(new RankingFilter());
        var only1001 = await ctx.Consolidated.RankingAsync(new RankingFilter(ClassroomId: ctx.School.C1001.ClassroomId));

        Assert.Equal(TestSchool.YearId, ranking.YearId);
        Assert.Equal(3, ranking.Threshold);
        Assert.Equal(["Paz, Beto", "Ruiz, Ana", "Sol, Dani"], ranking.Students.Select(s => s.StudentName));
        Assert.True(ranking.Students[0].ReachedThreshold);
        Assert.False(ranking.Students[1].ReachedThreshold);
        Assert.Equal(new ObservationCountsDto(1, 0, 1, 0, 2), ranking.Students[1].Counts);
        Assert.Equal(2, only1001.Students.Count);
    }

    [Fact]
    public async Task Student_consolidated_crosses_every_teacher_in_chronological_order()
    {
        var ctx = await CreateAsync();
        var first = await ObserveAsync(ctx, ctx.School.T1, ctx.School.Ana, CoexistenceTypes.TypeI, date: Today.AddDays(-5), subjectId: ctx.School.Physics.SubjectId);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, null, academic: true, date: Today.AddDays(-1), subjectId: ctx.School.Math.SubjectId);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI, date: Today.AddDays(-3));

        var teacher = new ObservationAccess(ctx.School.T1.Id, false);
        var met = await ctx.Observations.AddCommitmentAsync(teacher, first, new AddCommitmentRequest("Pedirá la palabra.", null));
        await ctx.Observations.AddCommitmentAsync(teacher, first, new AddCommitmentRequest("Traerá el material.", null));
        await ctx.Observations.SetCommitmentStatusAsync(teacher, met, new SetCommitmentStatusRequest(CommitmentStatuses.Met, null));

        var consolidated = await ctx.Consolidated.StudentAsync(ctx.School.Ana.StudentId, null);

        Assert.Equal("Ruiz, Ana", consolidated.StudentName);
        Assert.Equal("1001", consolidated.ClassroomName);
        Assert.Equal(TestSchool.YearId, consolidated.YearId);
        Assert.Equal(
            [Today.AddDays(-5), Today.AddDays(-3), Today.AddDays(-1)],
            consolidated.Observations.Select(o => o.Date));
        Assert.Equal(["Tomás Uno", "Tania Dos", "Tania Dos"], consolidated.Observations.Select(o => o.RecordedBy));
        Assert.Equal(new ObservationCountsDto(2, 0, 0, 1, 3), consolidated.Counts);
        Assert.Equal(new CommitmentSummaryDto(2, 1, 1, 0, 0), consolidated.Commitments);
        Assert.Equal(2, consolidated.Observations[0].Commitments.Count);
    }

    [Fact]
    public async Task List_filters_by_campus_and_grade()
    {
        var ctx = await CreateAsync();
        var coordination = new ObservationAccess(ctx.School.Marta.Id, true);
        await ObserveAsync(ctx, ctx.School.T2, ctx.School.Ana, CoexistenceTypes.TypeI);
        await ObserveAsync(ctx, ctx.School.T1, ctx.School.Dani, CoexistenceTypes.TypeI);

        var third = await ctx.Observations.ListAsync(coordination, new ObservationFilter(GradeId: ctx.School.C3A.GradeId, CoexistenceType: "I"));
        var campus = await ctx.Observations.ListAsync(coordination, new ObservationFilter(CampusId: ctx.School.Campus.CampusId));

        Assert.Equal("Sol, Dani", Assert.Single(third).StudentName);
        Assert.Equal(2, campus.Count);
    }
}
