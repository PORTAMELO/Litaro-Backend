using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AttendanceCalculationServiceTests
{
    private sealed class Scene
    {
        public required TestSchool School { get; init; }
        public required TestServices Services { get; init; }
        public RollAccess Admin => new(School.Marta.Id, false, true);
        public DateOnly Tuesday => School.Tuesday;
        public DateOnly NextTuesday => School.Tuesday.AddDays(7);

        public Task MissPhysicsAsync(DateOnly date, params Enrollment[] absent) =>
            Services.Roll.SaveSheetAsync(
                Admin,
                new SaveSheetRequest(School.C1001.ClassroomId, date, TestSchool.PhysicsStart, absent.Select(e => e.EnrollmentId).ToList(), []),
                date);

        public async Task<SummaryRowDto> PhysicsRowAsync(Enrollment student)
        {
            var summary = await Services.Calculation.GetSummaryAsync(
                School.Period.PeriodId, School.C1001.ClassroomId, School.Physics.SubjectId);

            return summary.Students.Single(s => s.EnrollmentId == student.EnrollmentId);
        }
    }

    private static async Task<Scene> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        return new Scene { School = school, Services = TestServices.Create(db) };
    }

    [Fact]
    public async Task Planned_hours_come_from_the_schedule()
    {
        var scene = await CreateAsync();

        var planned = await scene.Services.Calculation.PlannedHoursAsync(scene.School.C1001.ClassroomId, scene.School.Period);

        Assert.Equal(8m, planned[scene.School.Physics.SubjectId]);
        Assert.Equal(8m, planned[scene.School.Math.SubjectId]);
    }

    [Fact]
    public async Task One_unjustified_absence_of_eight_is_12_5_percent_and_no_loss()
    {
        var scene = await CreateAsync();

        await scene.MissPhysicsAsync(scene.Tuesday, scene.School.Beto);
        var row = await scene.PhysicsRowAsync(scene.School.Beto);

        Assert.Equal(1m, row.Unjustified);
        Assert.Equal(12.5m, row.Percent);
        Assert.False(row.Lost);
        Assert.Equal(0, await scene.School.Db.AbsenceLosses.CountAsync());
    }

    [Fact]
    public async Task Two_absences_of_eight_reach_the_limit_and_flag_the_loss()
    {
        var scene = await CreateAsync();

        await scene.MissPhysicsAsync(scene.Tuesday, scene.School.Beto);
        await scene.MissPhysicsAsync(scene.NextTuesday, scene.School.Beto);

        var row = await scene.PhysicsRowAsync(scene.School.Beto);
        var loss = await scene.School.Db.AbsenceLosses.SingleAsync();

        Assert.Equal(25m, row.Percent);
        Assert.True(row.Lost);
        Assert.Equal(scene.School.Beto.EnrollmentId, loss.EnrollmentId);
        Assert.Equal(scene.School.Physics.SubjectId, loss.SubjectId);
        Assert.Equal(scene.School.Period.PeriodId, loss.PeriodId);
        Assert.Equal(25m, loss.Percent);
        Assert.Equal(8m, loss.PlannedHours);
    }

    [Fact]
    public async Task An_excuse_removes_the_loss()
    {
        var scene = await CreateAsync();
        await scene.MissPhysicsAsync(scene.Tuesday, scene.School.Beto);
        await scene.MissPhysicsAsync(scene.NextTuesday, scene.School.Beto);

        await scene.Services.Excuses.CreateAsync(
            new SaveExcuseRequest(
                scene.School.Beto.StudentId,
                scene.NextTuesday.ToDateTime(new TimeOnly(6, 0)),
                scene.NextTuesday.ToDateTime(new TimeOnly(12, 30)),
                null),
            scene.School.Marta.Id,
            scene.NextTuesday);

        var row = await scene.PhysicsRowAsync(scene.School.Beto);

        Assert.Equal(12.5m, row.Percent);
        Assert.Equal(1m, row.Justified);
        Assert.False(row.Lost);
        Assert.Equal(0, await scene.School.Db.AbsenceLosses.CountAsync());
    }

    [Fact]
    public async Task Deleting_the_excuse_restores_the_loss()
    {
        var scene = await CreateAsync();
        await scene.MissPhysicsAsync(scene.Tuesday, scene.School.Beto);
        await scene.MissPhysicsAsync(scene.NextTuesday, scene.School.Beto);
        var id = await scene.Services.Excuses.CreateAsync(
            new SaveExcuseRequest(
                scene.School.Beto.StudentId,
                scene.NextTuesday.ToDateTime(new TimeOnly(6, 0)),
                scene.NextTuesday.ToDateTime(new TimeOnly(12, 30)),
                null),
            scene.School.Marta.Id,
            scene.NextTuesday);

        await scene.Services.Excuses.DeleteAsync(id);

        Assert.Equal(1, await scene.School.Db.AbsenceLosses.CountAsync());
    }

    [Fact]
    public async Task Correcting_the_list_removes_the_loss()
    {
        var scene = await CreateAsync();
        await scene.MissPhysicsAsync(scene.Tuesday, scene.School.Beto);
        await scene.MissPhysicsAsync(scene.NextTuesday, scene.School.Beto);

        await scene.MissPhysicsAsync(scene.NextTuesday);

        Assert.Equal(0, await scene.School.Db.AbsenceLosses.CountAsync());
    }

    [Fact]
    public async Task A_daily_absence_is_spread_over_that_days_subjects()
    {
        var scene = await CreateAsync();
        await scene.Services.Roll.SaveSheetAsync(
            scene.Admin,
            new SaveSheetRequest(scene.School.C3A.ClassroomId, scene.Tuesday, null, [scene.School.Dani.EnrollmentId], []),
            scene.Tuesday);

        var math = await scene.Services.Calculation.GetSummaryAsync(
            scene.School.Period.PeriodId, scene.School.C3A.ClassroomId, scene.School.Math.SubjectId);
        var chemistry = await scene.Services.Calculation.GetSummaryAsync(
            scene.School.Period.PeriodId, scene.School.C3A.ClassroomId, scene.School.Chemistry.SubjectId);

        Assert.Equal(1m, math.Students.Single().Unjustified);
        Assert.Equal(2m, chemistry.Students.Single().Unjustified);
        Assert.Equal(16m, chemistry.PlannedHours);
    }

    [Fact]
    public async Task A_subject_without_schedule_never_loses()
    {
        var scene = await CreateAsync();
        var arts = new Subject { Name = "Artes", WeeklyHours = 1, KnowledgeArea = "Artística" };
        scene.School.Db.Subjects.Add(arts);
        await scene.School.Db.SaveChangesAsync();
        await scene.MissPhysicsAsync(scene.Tuesday, scene.School.Beto);

        var summary = await scene.Services.Calculation.GetSummaryAsync(
            scene.School.Period.PeriodId, scene.School.C1001.ClassroomId, arts.SubjectId);

        Assert.Equal(0m, summary.PlannedHours);
        Assert.Equal(3, summary.Students.Count);
        Assert.All(summary.Students, row =>
        {
            Assert.Equal(0m, row.Percent);
            Assert.False(row.Lost);
        });
    }

    [Fact]
    public async Task A_closed_period_uses_its_frozen_hours()
    {
        var scene = await CreateAsync();
        scene.School.Db.PeriodPlannedHours.Add(new PeriodPlannedHours
        {
            PeriodId = scene.School.ClosedPeriod.PeriodId,
            ClassroomId = scene.School.C1001.ClassroomId,
            SubjectId = scene.School.Physics.SubjectId,
            PlannedHours = 10m,
        });
        scene.School.Db.Schedules.Remove(scene.School.PhysicsBlock);
        await scene.School.Db.SaveChangesAsync();

        var planned = await scene.Services.Calculation.PlannedHoursAsync(scene.School.C1001.ClassroomId, scene.School.ClosedPeriod);

        Assert.Equal(10m, planned[scene.School.Physics.SubjectId]);
    }

    [Fact]
    public async Task A_withdrawn_student_appears_only_with_absences()
    {
        var scene = await CreateAsync();
        await scene.MissPhysicsAsync(scene.Tuesday, scene.School.Beto);
        scene.School.Beto.Status = "RETIRED";
        scene.School.Caro.Status = "RETIRED";
        await scene.School.Db.SaveChangesAsync();

        var summary = await scene.Services.Calculation.GetSummaryAsync(
            scene.School.Period.PeriodId, scene.School.C1001.ClassroomId, scene.School.Physics.SubjectId);

        Assert.Contains(summary.Students, s => s.EnrollmentId == scene.School.Beto.EnrollmentId);
        Assert.DoesNotContain(summary.Students, s => s.EnrollmentId == scene.School.Caro.EnrollmentId);
        Assert.Equal(20m, summary.LossPercent);
    }

    [Fact]
    public async Task Teaches_is_true_only_for_the_assigned_teacher()
    {
        var scene = await CreateAsync();
        var calculation = scene.Services.Calculation;

        Assert.True(await calculation.TeachesAsync(scene.School.T1.Id, scene.School.C1001.ClassroomId, scene.School.Physics.SubjectId));
        Assert.False(await calculation.TeachesAsync(scene.School.T2.Id, scene.School.C1001.ClassroomId, scene.School.Physics.SubjectId));
    }
}
