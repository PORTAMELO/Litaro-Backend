using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class GradebookServiceTests
{
    private const PermissionFlags ReadCreate = PermissionFlags.Read | PermissionFlags.Create;
    private const PermissionFlags CreateDelete = PermissionFlags.Create | PermissionFlags.Delete;

    private static readonly DateTime Now = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static async Task<(TestSchool School, GradebookService Service, GradingDeadlineService Deadlines)> CreateAsync()
    {
        var school = await TestSchool.CreateAsync(TestDb.Create());
        var deadlines = new GradingDeadlineService(school.Db);
        var service = new GradebookService(school.Db, new AttendanceSettingsService(school.Db), deadlines);
        return (school, service, deadlines);
    }

    private static GradingAccess Teacher(User user, PermissionFlags book = ReadCreate, PermissionFlags design = CreateDelete) =>
        new(user.Id, book, design, PermissionFlags.None);

    private static SaveActivityRequest Activity(TestSchool school, Subject subject, string name, decimal? weight = null, short? periodId = null) =>
        new(null, school.C1001.ClassroomId, subject.SubjectId, periodId ?? school.Period.PeriodId, name, null, "Resuelve problemas", weight);

    private static Task<GradeSheetDto> Sheet(GradebookService service, TestSchool school, GradingAccess access, Subject? subject = null, short? periodId = null) =>
        service.GetSheetAsync(access, school.C1001.ClassroomId, (subject ?? school.Math).SubjectId, periodId ?? school.Period.PeriodId, Now, Today);

    private static int Component(GradeSheetDto sheet, string component) =>
        sheet.Activities.Single(a => a.Component == component).ActivityId;

    [Fact]
    public async Task Teacher_sees_only_own_courses_in_options()
    {
        var (school, service, _) = await CreateAsync();

        var own = await service.GetOptionsAsync(Teacher(school.T2), Now, Today);

        Assert.Equal(["Matemáticas", "Química"], own.Courses.Select(c => c.SubjectName).Order());
        Assert.All(own.Courses, c => Assert.True(c.Own));
        Assert.Equal(school.Period.PeriodId, own.CurrentPeriodId);
        Assert.True(own.Periods.Single(p => p.PeriodId == school.ClosedPeriod.PeriodId).Closed);

        var all = await service.GetOptionsAsync(Teacher(school.Marta, PermissionFlags.Full), Now, Today);

        Assert.Equal(5, all.Courses.Count);
    }

    [Fact]
    public async Task Options_use_the_active_year_that_contains_today()
    {
        var (school, service, _) = await CreateAsync();
        school.Db.AcademicYears.Add(new AcademicYear
        {
            YearId = 2027,
            StartDate = new DateTime(2027, 1, 18, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2027, 11, 26, 0, 0, 0, DateTimeKind.Utc),
            Active = true,
        });
        await school.Db.SaveChangesAsync();

        var options = await service.GetOptionsAsync(Teacher(school.T2), Now, Today);

        Assert.Equal(TestSchool.YearId, options.YearId);
        Assert.Equal(2, options.Periods.Count);
        Assert.Equal(2, options.Courses.Count);
    }

    [Fact]
    public async Task Sheet_creates_scheme_with_three_fixed_components()
    {
        var (school, service, _) = await CreateAsync();

        var sheet = await Sheet(service, school, Teacher(school.T2));

        Assert.Equal(GradingModes.Average, sheet.Mode);
        Assert.Equal([GradeComponents.Saber, GradeComponents.Self, GradeComponents.Peer], sheet.Activities.Select(a => a.Component));
        Assert.Equal(["Lima, Caro", "Paz, Beto", "Ruiz, Ana"], sheet.Students.Select(s => s.StudentName));
        Assert.True(sheet.CanEditScores);
        Assert.True(sheet.CanEditDesign);
        Assert.Equal(1, await school.Db.GradingSchemes.CountAsync());

        await Sheet(service, school, Teacher(school.T2));

        Assert.Equal(1, await school.Db.GradingSchemes.CountAsync());
    }

    [Fact]
    public async Task Sheet_rejects_unknown_classroom_or_subject()
    {
        var (school, service, _) = await CreateAsync();
        var admin = Teacher(school.Marta, PermissionFlags.Full);

        var noClassroom = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetSheetAsync(admin, 99999, school.Math.SubjectId, school.Period.PeriodId, Now, Today));
        var noSubject = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetSheetAsync(admin, school.C1001.ClassroomId, 99999, school.Period.PeriodId, Now, Today));

        Assert.Equal("El curso o la materia no existen.", noClassroom.Message);
        Assert.Equal("El curso o la materia no existen.", noSubject.Message);
    }

    [Fact]
    public async Task Teacher_cannot_open_foreign_sheet()
    {
        var (school, service, _) = await CreateAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Sheet(service, school, Teacher(school.T2), school.Physics));

        var sheet = await Sheet(service, school, Teacher(school.Marta, PermissionFlags.Full), school.Physics);

        Assert.Equal(3, sheet.Students.Count);
    }

    [Fact]
    public async Task Saving_scores_recalculates_the_row()
    {
        var (school, service, _) = await CreateAsync();
        var access = Teacher(school.T2);

        var sheet = await service.SaveActivityAsync(access, Activity(school, school.Math, "Taller"), Now, Today);
        var hetero = sheet.Activities.Single(a => a.Component == GradeComponents.Hetero).ActivityId;

        sheet = await service.SaveScoresAsync(access, new SaveScoresRequest(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId,
        [
            new(school.Ana.EnrollmentId, hetero, 40m),
            new(school.Ana.EnrollmentId, Component(sheet, GradeComponents.Saber), 45m),
            new(school.Ana.EnrollmentId, Component(sheet, GradeComponents.Self), 50m),
            new(school.Ana.EnrollmentId, Component(sheet, GradeComponents.Peer), 40m),
            new(school.Beto.EnrollmentId, hetero, 30m),
        ]), Now, Today);

        var ana = sheet.Students.Single(s => s.EnrollmentId == school.Ana.EnrollmentId);
        var beto = sheet.Students.Single(s => s.EnrollmentId == school.Beto.EnrollmentId);

        Assert.True(ana.Complete);
        Assert.Equal(4.15m, ana.Final);
        Assert.Equal("HIGH", ana.Performance);
        Assert.True(ana.Passing);
        Assert.False(beto.Complete);
        Assert.Null(beto.Final);
        Assert.Equal(3.00m, beto.Accumulated);
        Assert.False(beto.Passing);
        Assert.Equal(40m, ana.Scores[hetero]);

        sheet = await service.SaveScoresAsync(access, new SaveScoresRequest(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId,
            [new(school.Beto.EnrollmentId, hetero, null)]), Now, Today);

        Assert.Null(sheet.Students.Single(s => s.EnrollmentId == school.Beto.EnrollmentId).Accumulated);
    }

    [Fact]
    public async Task Invalid_batch_saves_nothing()
    {
        var (school, service, _) = await CreateAsync();
        var access = Teacher(school.T2);

        var sheet = await service.SaveActivityAsync(access, Activity(school, school.Math, "Taller"), Now, Today);
        var hetero = sheet.Activities.Single(a => a.Component == GradeComponents.Hetero).ActivityId;
        var chemistry = await service.SaveActivityAsync(access, Activity(school, school.Chemistry, "Laboratorio"), Now, Today);
        var foreign = chemistry.Activities.Single(a => a.Component == GradeComponents.Hetero).ActivityId;

        SaveScoresRequest Batch(params ScoreInput[] scores) =>
            new(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId, [.. scores]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveScoresAsync(access,
            Batch(new ScoreInput(school.Ana.EnrollmentId, hetero, 40m), new ScoreInput(school.Beto.EnrollmentId, hetero, 55m)), Now, Today));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveScoresAsync(access,
            Batch(new ScoreInput(school.Ana.EnrollmentId, hetero, 40m), new ScoreInput(school.Beto.EnrollmentId, foreign, 40m)), Now, Today));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveScoresAsync(access,
            Batch(new ScoreInput(school.Dani.EnrollmentId, hetero, 40m)), Now, Today));

        Assert.Equal(0, await school.Db.ActivityScores.CountAsync());
    }

    [Fact]
    public async Task Design_locked_blocks_teacher_but_not_update()
    {
        var (school, service, deadlines) = await CreateAsync();
        var current = await deadlines.GetAsync(TestSchool.YearId);
        await deadlines.SaveAsync(current with
        {
            Periods = current.Periods.Select(p => p with { DesignLockAt = p.PeriodId == school.Period.PeriodId ? Now.AddDays(-1) : null }).ToList(),
        }, school.Marta.Id, Now);

        var sheet = await Sheet(service, school, Teacher(school.T2));

        Assert.True(sheet.DesignLocked);
        Assert.False(sheet.CanEditDesign);
        Assert.True(sheet.CanEditScores);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SaveActivityAsync(Teacher(school.T2), Activity(school, school.Math, "Taller"), Now, Today));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SetModeAsync(Teacher(school.T2), new SetModeRequest(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId, GradingModes.Weighted), Now, Today));

        var updated = await service.SaveActivityAsync(Teacher(school.T2, design: PermissionFlags.Update), Activity(school, school.Math, "Taller"), Now, Today);

        Assert.Single(updated.Activities, a => a.Component == GradeComponents.Hetero);
    }

    [Fact]
    public async Task Closed_period_blocks_teacher_scores()
    {
        var (school, service, _) = await CreateAsync();
        var closed = school.ClosedPeriod.PeriodId;

        var sheet = await Sheet(service, school, Teacher(school.T2), periodId: closed);

        Assert.True(sheet.PeriodClosed);
        Assert.False(sheet.CanEditScores);

        var request = new SaveScoresRequest(school.C1001.ClassroomId, school.Math.SubjectId, closed,
            [new(school.Ana.EnrollmentId, Component(sheet, GradeComponents.Saber), 40m)]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveScoresAsync(Teacher(school.T2), request, Now, Today));

        var saved = await service.SaveScoresAsync(Teacher(school.T2, PermissionFlags.Read | PermissionFlags.Update), request, Now, Today);

        Assert.Equal(40m, saved.Students.Single(s => s.EnrollmentId == school.Ana.EnrollmentId).Scores[Component(sheet, GradeComponents.Saber)]);
    }

    [Fact]
    public async Task Replacement_teacher_sees_existing_design()
    {
        var (school, service, _) = await CreateAsync();

        await service.SaveActivityAsync(Teacher(school.T2), Activity(school, school.Math, "Taller"), Now, Today);

        var old = await school.Db.AcademicAssignments.SingleAsync(a =>
            a.ClassroomId == school.C1001.ClassroomId && a.SubjectId == school.Math.SubjectId && a.TeacherId == school.T2.Id);
        old.Active = false;
        school.Db.AcademicAssignments.Add(new AcademicAssignment
        {
            ClassroomId = school.C1001.ClassroomId,
            SubjectId = school.Math.SubjectId,
            TeacherId = school.T1.Id,
            YearId = TestSchool.YearId,
        });
        await school.Db.SaveChangesAsync();

        var sheet = await Sheet(service, school, Teacher(school.T1));

        Assert.Contains(sheet.Activities, a => a.Name == "Taller");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Sheet(service, school, Teacher(school.T2)));
    }

    [Fact]
    public async Task Absence_loss_sets_final_to_loss_grade()
    {
        var (school, service, _) = await CreateAsync();
        school.Db.AbsenceLosses.Add(new AbsenceLoss
        {
            EnrollmentId = school.Ana.EnrollmentId,
            SubjectId = school.Math.SubjectId,
            PeriodId = school.Period.PeriodId,
            UnjustifiedHours = 10,
            PlannedHours = 40,
            Percent = 25,
            FlaggedAt = Now,
        });
        await school.Db.SaveChangesAsync();

        var sheet = await Sheet(service, school, Teacher(school.T2));
        var ana = sheet.Students.Single(s => s.EnrollmentId == school.Ana.EnrollmentId);

        Assert.True(ana.AbsenceLoss);
        Assert.Equal(1.0m, ana.Final);
        Assert.False(ana.Passing);
    }

    [Fact]
    public async Task Weighted_mode_keeps_weights_and_reports_the_sum()
    {
        var (school, service, _) = await CreateAsync();
        var access = Teacher(school.T2);

        await service.SetModeAsync(access, new SetModeRequest(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId, GradingModes.Weighted), Now, Today);
        await service.SaveActivityAsync(access, Activity(school, school.Math, "Taller", 60m), Now, Today);
        var sheet = await service.SaveActivityAsync(access, Activity(school, school.Math, "Examen", 30m), Now, Today);

        Assert.Equal(GradingModes.Weighted, sheet.Mode);
        Assert.Equal(90m, sheet.WeightSum);

        await service.SetModeAsync(access, new SetModeRequest(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId, GradingModes.Average), Now, Today);
        sheet = await service.SetModeAsync(access, new SetModeRequest(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId, GradingModes.Weighted), Now, Today);

        Assert.Equal(0m, sheet.WeightSum);
        Assert.All(sheet.Activities.Where(a => a.Component == GradeComponents.Hetero), a => Assert.Null(a.Weight));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveActivityAsync(access, Activity(school, school.Math, "Quiz", 0m), Now, Today));
    }

    [Fact]
    public async Task Fixed_components_cannot_be_edited_or_deleted()
    {
        var (school, service, _) = await CreateAsync();
        var sheet = await Sheet(service, school, Teacher(school.T2));
        var saber = Component(sheet, GradeComponents.Saber);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteActivityAsync(Teacher(school.T2), saber, Now, Today));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveActivityAsync(Teacher(school.T2), Activity(school, school.Math, "Otro") with { ActivityId = saber }, Now, Today));
    }

    [Fact]
    public async Task Deleting_activity_removes_its_scores()
    {
        var (school, service, _) = await CreateAsync();
        var access = Teacher(school.T2);

        var sheet = await service.SaveActivityAsync(access, Activity(school, school.Math, "Taller"), Now, Today);
        var hetero = sheet.Activities.Single(a => a.Component == GradeComponents.Hetero).ActivityId;
        await service.SaveScoresAsync(access, new SaveScoresRequest(school.C1001.ClassroomId, school.Math.SubjectId, school.Period.PeriodId,
            [new(school.Ana.EnrollmentId, hetero, 40m)]), Now, Today);

        sheet = await service.DeleteActivityAsync(access, hetero, Now, Today);

        Assert.DoesNotContain(sheet.Activities, a => a.ActivityId == hetero);
        Assert.Equal(0, await school.Db.ActivityScores.CountAsync(s => s.GradeActivityId == hetero));
    }
}
