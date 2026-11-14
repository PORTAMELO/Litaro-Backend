using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class YearClosingServiceTests
{
    private sealed record Scene(AppDbContext Db, TestSchool School, YearClosingService Service, List<AcademicPeriod> Periods);

    private static DateTime Utc(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<Scene> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);

        school.ClosedPeriod.PeriodNumber = 1;
        school.ClosedPeriod.StartDate = Utc(1, 19);
        school.ClosedPeriod.EndDate = Utc(4, 17);
        school.Period.PeriodNumber = 2;
        school.Period.StartDate = Utc(4, 20);
        school.Period.EndDate = Utc(7, 31);
        var third = new AcademicPeriod { PeriodNumber = 3, StartDate = Utc(8, 3), EndDate = Utc(11, 27), YearId = TestSchool.YearId };
        db.AcademicPeriods.Add(third);
        await db.SaveChangesAsync();

        var settings = new AttendanceSettingsService(db);
        var service = new YearClosingService(db, new PromotionSettingsService(db), settings);
        return new Scene(db, school, service, [school.ClosedPeriod, school.Period, third]);
    }

    private static void Grade(Scene scene, Enrollment enrollment, Subject subject, AcademicPeriod period, decimal raw, Classroom? classroom = null)
    {
        var db = scene.Db;
        var classroomId = (classroom ?? scene.School.C1001).ClassroomId;
        var scheme = db.GradingSchemes
            .Include(s => s.Activities)
            .FirstOrDefault(s => s.ClassroomId == classroomId && s.SubjectId == subject.SubjectId && s.PeriodId == period.PeriodId);

        if (scheme is null)
        {
            scheme = new GradingScheme
            {
                ClassroomId = classroomId,
                SubjectId = subject.SubjectId,
                PeriodId = period.PeriodId,
                Mode = GradingModes.Average,
                UpdatedById = scene.School.T1.Id,
            };
            foreach (var component in new[] { GradeComponents.Hetero, GradeComponents.Saber, GradeComponents.Self, GradeComponents.Peer })
                scheme.Activities.Add(new GradeActivity { Component = component, Name = component, CreatedById = scene.School.T1.Id });
            db.GradingSchemes.Add(scheme);
            db.SaveChanges();
        }

        foreach (var activity in scheme.Activities)
        {
            db.ActivityScores.Add(new ActivityScore
            {
                GradeActivityId = activity.GradeActivityId,
                EnrollmentId = enrollment.EnrollmentId,
                Value = raw,
                RecordedById = scene.School.T1.Id,
            });
        }

        db.SaveChanges();
    }

    private static void GradeYear(Scene scene, Enrollment enrollment, decimal math, decimal chemistry, decimal physics, int periods = 3)
    {
        foreach (var period in scene.Periods.Take(periods))
        {
            Grade(scene, enrollment, scene.School.Math, period, math);
            Grade(scene, enrollment, scene.School.Chemistry, period, chemistry);
            Grade(scene, enrollment, scene.School.Physics, period, physics);
        }
    }

    private static async Task<Scene> GradedAsync()
    {
        var scene = await CreateAsync();
        GradeYear(scene, scene.School.Ana, 40m, 42m, 44m);
        GradeYear(scene, scene.School.Beto, 30m, 45m, 45m);
        GradeYear(scene, scene.School.Caro, 40m, 40m, 40m, periods: 2);
        return scene;
    }

    private static ClosingRowDto Row(ClosingPreviewDto preview, Enrollment enrollment) =>
        preview.Rows.Single(r => r.EnrollmentId == enrollment.EnrollmentId);

    [Fact]
    public async Task Preview_proposes_a_result_for_each_student()
    {
        var scene = await GradedAsync();
        using var db = scene.Db;

        var preview = await scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId);

        var ana = Row(preview, scene.School.Ana);
        Assert.Equal(PromotionRules.Promoted, ana.Proposed);
        Assert.Equal(4.30m, ana.Areas.Single(a => a.Area == "Naturales").Final);

        var beto = Row(preview, scene.School.Beto);
        Assert.Equal(PromotionRules.Failed, beto.Proposed);
        Assert.Equal(["Matemáticas"], beto.LostSubjects);

        Assert.Equal(PromotionRules.Pending, Row(preview, scene.School.Caro).Proposed);
    }

    [Fact]
    public async Task Close_updates_status_keeps_pending_students_active_and_logs_events()
    {
        var scene = await GradedAsync();
        using var db = scene.Db;

        var result = await scene.Service.CloseAsync(
            new CloseRequest(TestSchool.YearId, scene.School.C1001.ClassroomId, null), scene.School.Marta.Id);

        Assert.Equal(new CloseResultDto(1, 1, 1), result);

        var stored = await db.Enrollments.AsNoTracking().ToDictionaryAsync(e => e.EnrollmentId, e => e.Status);
        Assert.Equal(EnrollmentStatuses.Promoted, stored[scene.School.Ana.EnrollmentId]);
        Assert.Equal(EnrollmentStatuses.Failed, stored[scene.School.Beto.EnrollmentId]);
        Assert.Equal(EnrollmentStatuses.Active, stored[scene.School.Caro.EnrollmentId]);
        Assert.Equal(EnrollmentStatuses.Active, stored[scene.School.Dani.EnrollmentId]);

        var events = await db.EnrollmentEvents.AsNoTracking().ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(EnrollmentEventTypes.YearClose, e.Type));
        Assert.All(events, e => Assert.Equal(scene.School.Marta.Id, e.UserId));

        var again = await scene.Service.CloseAsync(
            new CloseRequest(TestSchool.YearId, scene.School.C1001.ClassroomId, null), scene.School.Marta.Id);
        Assert.Equal(new CloseResultDto(0, 0, 1), again);
    }

    [Fact]
    public async Task Commission_decision_needs_a_note_and_is_recorded()
    {
        var scene = await GradedAsync();
        using var db = scene.Db;
        var beto = scene.School.Beto.EnrollmentId;

        await Assert.ThrowsAsync<InvalidOperationException>(() => scene.Service.CloseAsync(
            new CloseRequest(TestSchool.YearId, scene.School.C1001.ClassroomId, [new CloseDecision(beto, PromotionRules.Promoted, " ")]),
            scene.School.Marta.Id));

        Assert.Equal(EnrollmentStatuses.Active, (await db.Enrollments.AsNoTracking().SingleAsync(e => e.EnrollmentId == beto)).Status);

        await scene.Service.CloseAsync(
            new CloseRequest(TestSchool.YearId, scene.School.C1001.ClassroomId, [new CloseDecision(beto, PromotionRules.Promoted, "Acta 12 de la Comisión")]),
            scene.School.Marta.Id);

        Assert.Equal(EnrollmentStatuses.Promoted, (await db.Enrollments.AsNoTracking().SingleAsync(e => e.EnrollmentId == beto)).Status);
        Assert.Equal("Acta 12 de la Comisión", (await db.EnrollmentEvents.AsNoTracking().SingleAsync(e => e.EnrollmentId == beto)).Note);

        var row = Row(await scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId), scene.School.Beto);
        Assert.Equal("Acta 12 de la Comisión", row.DecisionNote);
        Assert.Equal("Marta Rojas", row.DecidedBy);
    }

    [Fact]
    public async Task Absence_loss_replaces_the_period_grade()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        GradeYear(scene, scene.School.Ana, 50m, 50m, 50m);

        foreach (var period in scene.Periods)
        {
            db.AbsenceLosses.Add(new AbsenceLoss
            {
                EnrollmentId = scene.School.Ana.EnrollmentId,
                SubjectId = scene.School.Math.SubjectId,
                PeriodId = period.PeriodId,
            });
        }
        await db.SaveChangesAsync();

        var ana = Row(await scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId), scene.School.Ana);

        Assert.Equal(PromotionRules.Failed, ana.Proposed);
        Assert.Equal(1.00m, ana.Areas.Single(a => a.Area == "Exactas").Final);
    }

    [Fact]
    public async Task Grades_follow_the_student_after_a_classroom_change()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var other = new Classroom { Name = "1002", GradeId = scene.School.C1001.GradeId, CampusId = scene.School.Campus.CampusId };
        db.Classrooms.Add(other);
        await db.SaveChangesAsync();

        foreach (var subject in new[] { scene.School.Math, scene.School.Chemistry, scene.School.Physics })
        {
            Grade(scene, scene.School.Ana, subject, scene.Periods[0], 40m, other);
            Grade(scene, scene.School.Ana, subject, scene.Periods[1], 40m);
            Grade(scene, scene.School.Ana, subject, scene.Periods[2], 40m);
        }

        var ana = Row(await scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId), scene.School.Ana);

        Assert.Equal(PromotionRules.Promoted, ana.Proposed);
    }

    [Fact]
    public async Task Early_promoted_student_takes_first_period_from_the_second()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;

        foreach (var subject in new[] { scene.School.Math, scene.School.Chemistry, scene.School.Physics })
        {
            Grade(scene, ana, subject, scene.Periods[1], 40m);
            Grade(scene, ana, subject, scene.Periods[2], 40m);
        }

        var before = Row(await scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId), ana);
        Assert.Equal(PromotionRules.Pending, before.Proposed);

        db.EnrollmentEvents.Add(new EnrollmentEvent
        {
            EnrollmentId = ana.EnrollmentId,
            Type = EnrollmentEventTypes.EarlyPromotion,
            FromClassroomId = scene.School.C3A.ClassroomId,
            ToClassroomId = scene.School.C1001.ClassroomId,
            UserId = scene.School.Marta.Id,
        });
        await db.SaveChangesAsync();

        var after = Row(await scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId), ana);
        Assert.Equal(PromotionRules.Promoted, after.Proposed);
    }

    [Fact]
    public async Task Student_who_failed_last_year_is_marked_as_repeater()
    {
        var scene = await GradedAsync();
        using var db = scene.Db;

        db.AcademicYears.Add(new AcademicYear { YearId = 2025, StartDate = Utc(1, 1), EndDate = Utc(1, 2), Active = false });
        db.Enrollments.Add(new Enrollment
        {
            StudentId = scene.School.Ana.StudentId,
            ClassroomId = scene.School.C1001.ClassroomId,
            YearId = 2025,
            Status = EnrollmentStatuses.Failed,
        });
        await db.SaveChangesAsync();

        var preview = await scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId);

        Assert.True(Row(preview, scene.School.Ana).Repeater);
        Assert.False(Row(preview, scene.School.Beto).Repeater);
    }

    [Fact]
    public async Task Year_without_the_three_periods_is_rejected()
    {
        var scene = await GradedAsync();
        using var db = scene.Db;
        db.AcademicPeriods.Add(new AcademicPeriod { PeriodNumber = 4, StartDate = Utc(11, 28), EndDate = Utc(12, 4), YearId = TestSchool.YearId });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.PreviewAsync(TestSchool.YearId, scene.School.C1001.ClassroomId));
    }

    [Fact]
    public async Task Settings_are_saved_and_validated()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var service = new PromotionSettingsService(db);

        Assert.Equal(PromotionSettingsService.Defaults.Period3Weight, (await service.GetAsync()).Period3Weight);

        var saved = await service.SaveAsync(PromotionSettingsService.Defaults with
        {
            Period1Weight = 25m,
            Period2Weight = 25m,
            Period3Weight = 50m,
            FundamentalSubjectIds = [scene.School.Math.SubjectId],
        });

        Assert.Equal(50m, saved.Period3Weight);
        Assert.Equal([scene.School.Math.SubjectId], saved.FundamentalSubjectIds);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(PromotionSettingsService.Defaults with { Period1Weight = 50m }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(PromotionSettingsService.Defaults with { FundamentalSubjectIds = [9999] }));
    }

    [Fact]
    public async Task Early_promotion_moves_the_student_to_the_next_grade()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var school = scene.School;

        var fourth = new Grade { Name = "Cuarto", OrderNum = 4, Level = "Primaria" };
        db.Grades.Add(fourth);
        await db.SaveChangesAsync();
        var c4a = new Classroom { Name = "4A", GradeId = fourth.GradeId, CampusId = school.Campus.CampusId };
        db.Classrooms.Add(c4a);
        await db.SaveChangesAsync();
        TestData.AddNotificationType(db, NotificationCodes.SimatClassroomChange, "{estudiante} {origen} {destino} {sede} {documento}");

        var movements = new StudentMovementService(db, new NotificationService(db));
        var firstPeriod = new DateOnly(2026, 3, 30);
        var dani = school.Dani.EnrollmentId;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            movements.PromoteEarlyAsync(dani, c4a.ClassroomId, null, school.Marta.Id, firstPeriod));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            movements.PromoteEarlyAsync(dani, c4a.ClassroomId, "Acta 3", school.Marta.Id, new DateOnly(2026, 5, 4)));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            movements.PromoteEarlyAsync(dani, school.C1001.ClassroomId, "Acta 3", school.Marta.Id, firstPeriod));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            movements.PromoteEarlyAsync(school.Ana.EnrollmentId, c4a.ClassroomId, "Acta 3", school.Marta.Id, firstPeriod));

        await movements.PromoteEarlyAsync(dani, c4a.ClassroomId, "Acta 3 del Consejo Directivo", school.Marta.Id, firstPeriod);

        Assert.Equal(c4a.ClassroomId, (await db.Enrollments.AsNoTracking().SingleAsync(e => e.EnrollmentId == dani)).ClassroomId);
        var movement = await db.EnrollmentEvents.AsNoTracking().SingleAsync();
        Assert.Equal(EnrollmentEventTypes.EarlyPromotion, movement.Type);
        Assert.Equal(school.C3A.ClassroomId, movement.FromClassroomId);
        Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Pre_enrollment_keeps_failed_students_in_the_same_grade()
    {
        var scene = await GradedAsync();
        using var db = scene.Db;
        var school = scene.School;

        var eleventh = new Grade { Name = "Once", OrderNum = 11, Level = "Media" };
        db.Grades.Add(eleventh);
        db.AcademicYears.Add(new AcademicYear { YearId = 2027, StartDate = Utc(1, 1), EndDate = Utc(1, 2), Active = false });
        db.EnrollmentPeriods.Add(new EnrollmentPeriod { YearId = 2027 });
        await db.SaveChangesAsync();

        await scene.Service.CloseAsync(new CloseRequest(TestSchool.YearId, school.C1001.ClassroomId, null), school.Marta.Id);

        var notifications = new NotificationService(db);
        var preEnrollment = new PreEnrollmentService(db, new EnrollmentPeriodService(db), notifications);
        await preEnrollment.GenerateAsync(2027);

        var reserved = await db.PreEnrollments.AsNoTracking().ToDictionaryAsync(p => p.StudentId, p => p.GradeId);
        Assert.Equal(eleventh.GradeId, reserved[school.Ana.StudentId]);
        Assert.Equal(school.C1001.GradeId, reserved[school.Beto.StudentId]);
        Assert.Equal(eleventh.GradeId, reserved[school.Caro.StudentId]);
    }
}
