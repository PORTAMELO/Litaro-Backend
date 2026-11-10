using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class AttendanceExcuseServiceTests
{
    private sealed class Scene
    {
        public required TestSchool School { get; init; }
        public required TestServices Services { get; init; }
        public RollAccess Admin => new(School.Marta.Id, false, true);
        public DateOnly Tuesday => School.Tuesday;
        public DateOnly NextTuesday => School.Tuesday.AddDays(7);

        public Task SaveAsync(DateOnly date, TimeOnly start, params Enrollment[] absent) =>
            Services.Roll.SaveSheetAsync(
                Admin,
                new SaveSheetRequest(School.C1001.ClassroomId, date, start, absent.Select(e => e.EnrollmentId).ToList(), []),
                date);

        public DateTime At(DateOnly date, int hour, int minute) => date.ToDateTime(new TimeOnly(hour, minute));

        public SaveExcuseRequest Excuse(Enrollment student, DateTime from, DateTime to, string? reason = null) =>
            new(student.StudentId, from, to, reason);

        public Task<List<AttendanceAbsence>> AbsencesAsync(Enrollment student) =>
            School.Db.AttendanceAbsences
                .Include(a => a.Session)
                .Where(a => a.EnrollmentId == student.EnrollmentId)
                .OrderBy(a => a.Session.Date)
                .ThenBy(a => a.Session.StartTime)
                .ToListAsync();
    }

    private static async Task<Scene> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        return new Scene { School = school, Services = TestServices.Create(db) };
    }

    [Fact]
    public async Task An_excuse_justifies_the_absences_inside_its_range()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(scene.Tuesday, TestSchool.ChemistryStart, scene.School.Beto);
        await scene.SaveAsync(scene.Tuesday, TestSchool.PhysicsStart, scene.School.Beto);
        await scene.SaveAsync(scene.NextTuesday, TestSchool.PhysicsStart, scene.School.Beto);

        var id = await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 6, 0), scene.At(scene.Tuesday, 12, 30), "Cita médica"),
            scene.School.Marta.Id,
            scene.Tuesday);

        var absences = await scene.AbsencesAsync(scene.School.Beto);

        Assert.Equal(new int?[] { id, id, null }, absences.Select(a => a.AttendanceExcuseId));
    }

    [Fact]
    public async Task An_evasion_is_justified_too_and_keeps_its_status()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(scene.Tuesday, TestSchool.ChemistryStart);
        await scene.SaveAsync(scene.Tuesday, TestSchool.PhysicsStart, scene.School.Beto);

        await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 6, 0), scene.At(scene.Tuesday, 12, 30)),
            scene.School.Marta.Id,
            scene.Tuesday);

        var absence = (await scene.AbsencesAsync(scene.School.Beto)).Single();

        Assert.NotNull(absence.AttendanceExcuseId);
        Assert.Equal(AbsenceStatuses.Evasion, absence.Status);
    }

    [Fact]
    public async Task A_partial_day_range_only_covers_the_classes_inside()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(scene.Tuesday, TestSchool.ChemistryStart, scene.School.Beto);
        await scene.SaveAsync(scene.Tuesday, TestSchool.PhysicsStart, scene.School.Beto);

        await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 8, 30), scene.At(scene.Tuesday, 12, 30)),
            scene.School.Marta.Id,
            scene.Tuesday);

        var absences = await scene.AbsencesAsync(scene.School.Beto);

        Assert.Null(absences[0].AttendanceExcuseId);
        Assert.NotNull(absences[1].AttendanceExcuseId);
    }

    [Fact]
    public async Task A_daily_absence_is_covered_by_its_date()
    {
        var scene = await CreateAsync();
        await scene.Services.Roll.SaveSheetAsync(
            scene.Admin,
            new SaveSheetRequest(scene.School.C3A.ClassroomId, scene.Tuesday, null, [scene.School.Dani.EnrollmentId], []),
            scene.Tuesday);

        await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Dani, scene.At(scene.Tuesday, 10, 0), scene.At(scene.Tuesday, 11, 0)),
            scene.School.Marta.Id,
            scene.Tuesday);

        Assert.NotNull((await scene.AbsencesAsync(scene.School.Dani)).Single().AttendanceExcuseId);
    }

    [Fact]
    public async Task An_expired_excuse_is_rejected_with_the_deadline()
    {
        var scene = await CreateAsync();
        var october = new DateOnly(2026, 10, 6);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Excuses.CreateAsync(
                scene.Excuse(scene.School.Beto, scene.At(october, 6, 0), scene.At(october, 12, 30)),
                scene.School.Marta.Id,
                scene.Tuesday));

        Assert.Equal("El plazo para presentar esta excusa venció el 08/10/2026.", error.Message);
        Assert.Equal(0, await scene.School.Db.AttendanceExcuses.CountAsync());
    }

    [Fact]
    public async Task An_excuse_on_the_last_day_of_the_deadline_is_accepted()
    {
        var scene = await CreateAsync();
        var october = new DateOnly(2026, 10, 6);

        var id = await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Beto, scene.At(october, 6, 0), scene.At(october, 12, 30)),
            scene.School.Marta.Id,
            new DateOnly(2026, 10, 8));

        Assert.True(id > 0);
    }

    [Fact]
    public async Task Preview_reports_expiry_without_throwing()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(scene.Tuesday, TestSchool.PhysicsStart, scene.School.Beto);
        var request = scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 6, 0), scene.At(scene.Tuesday, 12, 30));

        var onTime = await scene.Services.Excuses.PreviewAsync(request, scene.Tuesday);
        var late = await scene.Services.Excuses.PreviewAsync(request, scene.Tuesday.AddDays(10));

        Assert.Equal(1, onTime.Absences);
        Assert.False(onTime.Expired);
        Assert.Equal(new DateOnly(2026, 11, 12), onTime.Deadline);
        Assert.True(late.Expired);
    }

    [Fact]
    public async Task Deleting_an_excuse_restores_the_absences()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(scene.Tuesday, TestSchool.PhysicsStart, scene.School.Beto);
        var id = await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 6, 0), scene.At(scene.Tuesday, 12, 30)),
            scene.School.Marta.Id,
            scene.Tuesday);

        var affected = await scene.Services.Excuses.DeleteAsync(id);

        Assert.Equal(new[] { scene.School.Beto.EnrollmentId }, affected);
        Assert.Null((await scene.AbsencesAsync(scene.School.Beto)).Single().AttendanceExcuseId);
        Assert.Equal(0, await scene.School.Db.AttendanceExcuses.CountAsync());
        Assert.Empty(await scene.Services.Excuses.DeleteAsync(id));
    }

    [Fact]
    public async Task A_later_absence_inside_a_valid_excuse_is_justified_on_save()
    {
        var scene = await CreateAsync();
        var id = await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 6, 0), scene.At(scene.Tuesday, 18, 0)),
            scene.School.Marta.Id,
            scene.Tuesday);

        await scene.SaveAsync(scene.Tuesday, TestSchool.PhysicsStart, scene.School.Beto, scene.School.Ana);

        Assert.Equal(id, (await scene.AbsencesAsync(scene.School.Beto)).Single().AttendanceExcuseId);
        Assert.Null((await scene.AbsencesAsync(scene.School.Ana)).Single().AttendanceExcuseId);
    }

    [Fact]
    public async Task End_must_be_after_start()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Excuses.CreateAsync(
                scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 12, 0), scene.At(scene.Tuesday, 12, 0)),
                scene.School.Marta.Id,
                scene.Tuesday));

        Assert.Equal("La fecha de fin debe ser posterior a la de inicio.", error.Message);
    }

    [Fact]
    public async Task An_unknown_student_is_rejected()
    {
        var scene = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scene.Services.Excuses.CreateAsync(
                new SaveExcuseRequest(98765, scene.At(scene.Tuesday, 6, 0), scene.At(scene.Tuesday, 12, 0), null),
                scene.School.Marta.Id,
                scene.Tuesday));

        Assert.Equal("No existe el estudiante.", error.Message);
    }

    [Fact]
    public async Task The_list_shows_student_classroom_and_absences()
    {
        var scene = await CreateAsync();
        await scene.SaveAsync(scene.Tuesday, TestSchool.PhysicsStart, scene.School.Beto);
        await scene.Services.Excuses.CreateAsync(
            scene.Excuse(scene.School.Beto, scene.At(scene.Tuesday, 6, 0), scene.At(scene.Tuesday, 12, 30), "  Cita médica  "),
            scene.School.Marta.Id,
            scene.Tuesday);

        var row = (await scene.Services.Excuses.ListAsync(TestSchool.YearId, "paz")).Single();

        Assert.Equal("Paz, Beto", row.StudentName);
        Assert.Equal("1001", row.ClassroomName);
        Assert.Equal("Cita médica", row.Reason);
        Assert.Equal(1, row.Absences);
        Assert.Equal("Marta Rojas", row.CreatedBy);
        Assert.Empty(await scene.Services.Excuses.ListAsync(TestSchool.YearId, "zzz"));
    }
}
