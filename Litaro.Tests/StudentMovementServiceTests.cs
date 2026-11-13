using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class StudentMovementServiceTests
{
    private const string ClassroomTemplate =
        "{estudiante} ({documento}) pasó del curso {origen} al curso {destino} en {sede}. Actualice el grupo en el SIMAT.";

    private const string CampusTemplate =
        "{estudiante} ({documento}) fue trasladado de {origen} ({sede_origen}) a {destino} ({sede_destino}). Registre el traslado entre sedes en el SIMAT.";

    private const string WithdrawalTemplate =
        "{estudiante} ({documento}) fue retirado del curso {curso} en {anio}. Motivo: {motivo}. Registre el retiro en el SIMAT.";

    private sealed record Scene(
        AppDbContext Db,
        TestSchool School,
        StudentMovementService Service,
        Classroom SameCampus,
        Classroom OtherCampus,
        Campus Cayunda);

    private static async Task<Scene> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);

        db.Schools.Add(new School { Name = "IED Instituto Nacional de Promoción Social", Nit = "800" });

        var cayunda = new Campus { Name = "Cayundá", Address = "Calle 9", Dane = "125875000427", SchoolId = 1 };
        db.Campuses.Add(cayunda);
        await db.SaveChangesAsync();

        var sameCampus = new Classroom { Name = "1002", GradeId = school.C1001.GradeId, CampusId = school.Campus.CampusId };
        var otherCampus = new Classroom { Name = "1001-C", GradeId = school.C1001.GradeId, CampusId = cayunda.CampusId };
        db.AddRange(sameCampus, otherCampus);

        var reasons = new Characteristic { Name = StudentMovementService.ReasonsCharacteristic };
        db.Characteristics.Add(reasons);
        await db.SaveChangesAsync();

        db.CharacteristicDetails.AddRange(
            new CharacteristicDetail { CharacteristicId = reasons.CharacteristicId, Nombre = "Motivo", Valor = "Deserción" },
            new CharacteristicDetail { CharacteristicId = reasons.CharacteristicId, Nombre = "Motivo", Valor = "Traslado" });
        await db.SaveChangesAsync();

        TestData.AddNotificationType(db, NotificationCodes.SimatClassroomChange, ClassroomTemplate);
        TestData.AddNotificationType(db, NotificationCodes.SimatCampusTransfer, CampusTemplate);
        TestData.AddNotificationType(db, NotificationCodes.SimatWithdrawal, WithdrawalTemplate);

        return new Scene(db, school, new StudentMovementService(db, new NotificationService(db)), sameCampus, otherCampus, cayunda);
    }

    private static string Document(AppDbContext db, Enrollment enrollment) =>
        db.Users.Single(u => u.Id == enrollment.StudentId).DocumentNumber;

    [Fact]
    public async Task Classroom_change_moves_the_student_and_keeps_the_history()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;

        db.GradeScores.Add(new GradeScore
        {
            EnrollmentId = ana.EnrollmentId,
            SubjectId = scene.School.Math.SubjectId,
            PeriodId = scene.School.ClosedPeriod.PeriodId,
            Value = 4.2m,
        });
        await db.SaveChangesAsync();

        await scene.Service.ChangeClassroomAsync(ana.EnrollmentId, scene.SameCampus.ClassroomId, " Solicitud del acudiente ", scene.School.Marta.Id);

        var stored = await db.Enrollments.AsNoTracking().SingleAsync(e => e.EnrollmentId == ana.EnrollmentId);
        Assert.Equal(scene.SameCampus.ClassroomId, stored.ClassroomId);
        Assert.Equal(EnrollmentStatuses.Active, stored.Status);

        var score = await db.GradeScores.AsNoTracking().SingleAsync(g => g.EnrollmentId == ana.EnrollmentId);
        Assert.Equal(4.2m, score.Value);

        var history = await scene.Service.GetEventsAsync(ana.EnrollmentId);
        var movement = Assert.Single(history);
        Assert.Equal(EnrollmentEventTypes.Classroom, movement.Type);
        Assert.Equal("1001", movement.FromClassroom);
        Assert.Equal("1002", movement.ToClassroom);
        Assert.Equal("Solicitud del acudiente", movement.Note);
        Assert.Equal("Marta Rojas", movement.UserName);
    }

    [Fact]
    public async Task Classroom_change_alerts_the_simat_recipients()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;

        await scene.Service.ChangeClassroomAsync(ana.EnrollmentId, scene.SameCampus.ClassroomId, null, scene.School.Marta.Id);

        var notification = await db.Notifications.SingleAsync();
        Assert.Equal(
            $"Ana Ruiz ({Document(db, ana)}) pasó del curso 1001 al curso 1002 en Sede Principal. Actualice el grupo en el SIMAT.",
            notification.Message);
        Assert.Equal(ana.StudentId, notification.StudentId);
    }

    [Fact]
    public async Task Classroom_change_rejects_another_campus_another_grade_and_the_same_classroom()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;
        var userId = scene.School.Marta.Id;

        var otherCampus = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.ChangeClassroomAsync(ana.EnrollmentId, scene.OtherCampus.ClassroomId, null, userId));
        Assert.Contains("otra sede", otherCampus.Message);

        var otherGrade = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.ChangeClassroomAsync(ana.EnrollmentId, scene.School.C3A.ClassroomId, null, userId));
        Assert.Contains("mismo grado", otherGrade.Message);

        var same = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.ChangeClassroomAsync(ana.EnrollmentId, scene.School.C1001.ClassroomId, null, userId));
        Assert.Contains("ya está en ese curso", same.Message);

        Assert.Empty(db.EnrollmentEvents);
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task Campus_transfer_moves_the_student_and_builds_the_certificate()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;

        var eventId = await scene.Service.TransferCampusAsync(ana.EnrollmentId, scene.OtherCampus.ClassroomId, null, scene.School.Marta.Id);

        var stored = await db.Enrollments.AsNoTracking().SingleAsync(e => e.EnrollmentId == ana.EnrollmentId);
        Assert.Equal(scene.OtherCampus.ClassroomId, stored.ClassroomId);
        Assert.Equal(scene.Cayunda.CampusId, db.Users.Single(u => u.Id == ana.StudentId).CampusId);

        var certificate = await scene.Service.GetCertificateAsync(eventId);
        Assert.NotNull(certificate);
        Assert.Equal("IED Instituto Nacional de Promoción Social", certificate.SchoolName);
        Assert.Equal("Ana Ruiz", certificate.StudentName);
        Assert.Equal("Décimo", certificate.GradeName);
        Assert.Equal("Sede Principal", certificate.FromCampus);
        Assert.Equal("125875000001", certificate.FromDane);
        Assert.Equal("Cayundá", certificate.ToCampus);
        Assert.Equal("125875000427", certificate.ToDane);

        var notification = await db.Notifications.SingleAsync();
        Assert.Equal(
            $"Ana Ruiz ({Document(db, ana)}) fue trasladado de 1001 (Sede Principal) a 1001-C (Cayundá). Registre el traslado entre sedes en el SIMAT.",
            notification.Message);
    }

    [Fact]
    public async Task Campus_transfer_rejects_a_classroom_of_the_same_campus()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.TransferCampusAsync(scene.School.Ana.EnrollmentId, scene.SameCampus.ClassroomId, null, scene.School.Marta.Id));

        Assert.Contains("misma sede", error.Message);
    }

    [Fact]
    public async Task Certificate_only_exists_for_campus_transfers()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;

        var eventId = await scene.Service.ChangeClassroomAsync(
            scene.School.Ana.EnrollmentId, scene.SameCampus.ClassroomId, null, scene.School.Marta.Id);

        Assert.Null(await scene.Service.GetCertificateAsync(eventId));
    }

    [Fact]
    public async Task Withdrawal_marks_the_enrollment_and_keeps_every_record()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;

        db.GradeScores.Add(new GradeScore
        {
            EnrollmentId = ana.EnrollmentId,
            SubjectId = scene.School.Math.SubjectId,
            PeriodId = scene.School.ClosedPeriod.PeriodId,
            Value = 3.1m,
        });
        await db.SaveChangesAsync();

        await scene.Service.WithdrawAsync(ana.EnrollmentId, "deserción", "No volvió desde octubre", scene.School.Marta.Id);

        var stored = await db.Enrollments.AsNoTracking().SingleAsync(e => e.EnrollmentId == ana.EnrollmentId);
        Assert.Equal(EnrollmentStatuses.Withdrawn, stored.Status);
        Assert.Equal("Deserción", stored.StatusReason);
        Assert.Equal(scene.School.C1001.ClassroomId, stored.ClassroomId);
        Assert.Single(db.GradeScores.Where(g => g.EnrollmentId == ana.EnrollmentId));
        Assert.True(db.Students.Single(s => s.StudentId == ana.StudentId).Active);

        var movement = Assert.Single(await scene.Service.GetEventsAsync(ana.EnrollmentId));
        Assert.Equal(EnrollmentEventTypes.Withdrawal, movement.Type);
        Assert.Equal("Deserción", movement.Reason);
        Assert.Null(movement.ToClassroom);

        var notification = await db.Notifications.SingleAsync();
        Assert.Equal(
            $"Ana Ruiz ({Document(db, ana)}) fue retirado del curso 1001 en 2026. Motivo: Deserción. Registre el retiro en el SIMAT.",
            notification.Message);
    }

    [Theory]
    [InlineData(null, "Seleccione el motivo")]
    [InlineData("  ", "Seleccione el motivo")]
    [InlineData("Se aburrió", "catálogo de motivos")]
    public async Task Withdrawal_requires_a_reason_from_the_simat_catalog(string? reason, string expected)
    {
        var scene = await CreateAsync();
        using var db = scene.Db;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.WithdrawAsync(scene.School.Ana.EnrollmentId, reason, null, scene.School.Marta.Id));

        Assert.Contains(expected, error.Message);
        Assert.Equal(EnrollmentStatuses.Active, db.Enrollments.Single(e => e.EnrollmentId == scene.School.Ana.EnrollmentId).Status);
    }

    [Fact]
    public async Task A_withdrawn_student_cannot_be_moved_or_withdrawn_again()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;
        var userId = scene.School.Marta.Id;

        await scene.Service.WithdrawAsync(ana.EnrollmentId, "Traslado", null, userId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.ChangeClassroomAsync(ana.EnrollmentId, scene.SameCampus.ClassroomId, null, userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.TransferCampusAsync(ana.EnrollmentId, scene.OtherCampus.ClassroomId, null, userId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.WithdrawAsync(ana.EnrollmentId, "Traslado", null, userId));

        Assert.Single(db.EnrollmentEvents);
    }

    [Fact]
    public async Task Movements_are_blocked_once_the_year_is_no_longer_active()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;

        db.AcademicYears.Single(y => y.YearId == TestSchool.YearId).Active = false;
        await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Service.ChangeClassroomAsync(scene.School.Ana.EnrollmentId, scene.SameCampus.ClassroomId, null, scene.School.Marta.Id));

        Assert.Contains("ya no está activo", error.Message);
    }

    [Fact]
    public async Task Search_finds_students_by_name_document_or_code_and_shows_withdrawn_ones()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;
        var ana = scene.School.Ana;

        await scene.Service.WithdrawAsync(ana.EnrollmentId, "Traslado", null, scene.School.Marta.Id);

        var byName = Assert.Single(await scene.Service.SearchAsync("ana ru"));
        Assert.Equal(EnrollmentStatuses.Withdrawn, byName.Status);
        Assert.Equal("Traslado", byName.StatusReason);
        Assert.Equal("Sede Principal", byName.CampusName);

        Assert.Single(await scene.Service.SearchAsync(Document(db, ana)));
        Assert.Single(await scene.Service.SearchAsync($"S-{ana.StudentId}"));
        Assert.Empty(await scene.Service.SearchAsync("a"));
    }

    [Fact]
    public async Task Options_list_active_classrooms_and_the_withdrawal_reasons()
    {
        var scene = await CreateAsync();
        using var db = scene.Db;

        scene.SameCampus.Active = false;
        await db.SaveChangesAsync();

        var options = await scene.Service.GetOptionsAsync();

        Assert.Equal(new[] { "Deserción", "Traslado" }, options.WithdrawalReasons);
        Assert.Equal(2, options.Campuses.Count);
        Assert.DoesNotContain(options.Classrooms, c => c.ClassroomId == scene.SameCampus.ClassroomId);
        Assert.Contains(options.Classrooms, c => c.ClassroomId == scene.OtherCampus.ClassroomId);
    }

    [Fact]
    public async Task Only_the_role_with_the_process_permission_can_execute_movements()
    {
        using var db = TestDb.Create();
        var coordinator = TestData.AddRole(db, "Coordinador Académico");
        TestData.AddRole(db, "Rector");
        TestData.AddRole(db, "Administrador");
        db.RolePermissions.Add(new RolePermission { RoleId = coordinator.Id, TableName = ProcessKeys.StudentMovement, Permissions = 5 });
        await db.SaveChangesAsync();
        var permissions = new PermissionService(db);

        Assert.True(await permissions.HasPermissionAsync(new[] { "Coordinador Académico" }, ProcessKeys.StudentMovement, PermissionFlags.Update));
        Assert.True(await permissions.HasPermissionAsync(new[] { "Coordinador Académico" }, ProcessKeys.StudentMovement, PermissionFlags.Read));
        Assert.False(await permissions.HasPermissionAsync(new[] { "Rector" }, ProcessKeys.StudentMovement, PermissionFlags.Update));
        Assert.False(await permissions.HasPermissionAsync(new[] { "Administrador" }, ProcessKeys.StudentMovement, PermissionFlags.Update));
    }
}
