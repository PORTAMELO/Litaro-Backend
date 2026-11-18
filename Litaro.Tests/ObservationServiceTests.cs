using Litaro.Models;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class ObservationServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateTime Now = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    private static async Task<(TestSchool School, ObservationService Service)> CreateAsync()
    {
        var db = TestDb.Create();
        var school = await TestSchool.CreateAsync(db);
        return (school, new ObservationService(db, new DocumentService(db, new FakeDocumentStorage()), new CitationService(db, new NotificationService(db)), new ObservationConsolidatedService(db, new ObservationSettingsService(db), new NotificationService(db))));
    }

    private static ObservationAccess Teacher(User user) => new(user.Id, false);

    private static ObservationAccess Coordination(TestSchool school) => new(school.Marta.Id, true);

    private static SaveObservationRequest Request(
        Enrollment enrollment,
        bool academic = false,
        bool coexistence = true,
        string? type = CoexistenceTypes.TypeI,
        string description = "Interrumpe la clase de forma reiterada.",
        DateOnly? date = null) =>
        new(enrollment.EnrollmentId, date ?? Today, description, academic, coexistence, type, null);

    [Fact]
    public async Task Options_use_the_active_year_that_contains_today()
    {
        var (school, service) = await CreateAsync();
        var now = DateTime.UtcNow;
        var current = await school.Db.AcademicYears.SingleAsync();
        current.StartDate = now.AddDays(-30);
        current.EndDate = now.AddDays(30);
        school.Db.AcademicYears.Add(new AcademicYear
        {
            YearId = TestSchool.YearId + 1,
            StartDate = now.AddDays(60),
            EndDate = now.AddDays(400),
            Active = true,
        });
        await school.Db.SaveChangesAsync();

        var options = await service.GetOptionsAsync(Coordination(school));

        Assert.Equal(TestSchool.YearId, options.YearId);
        Assert.NotEmpty(options.Classrooms);
        Assert.Equal([(short)(TestSchool.YearId + 1), TestSchool.YearId], options.Years);
        Assert.All(options.Classrooms, classroom => Assert.Equal(school.Campus.CampusId, classroom.CampusId));
    }

    [Fact]
    public async Task Coexistence_observation_requires_a_type()
    {
        var (school, service) = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Teacher(school.T2), Request(school.Ana, type: null), Today));

        Assert.Equal("La observación de convivencia debe tener tipificación Tipo I, II o III.", error.Message);
        Assert.Empty(school.Db.Observations);
    }

    [Fact]
    public async Task Observation_must_be_academic_coexistence_or_both()
    {
        var (school, service) = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Teacher(school.T2), Request(school.Ana, academic: false, coexistence: false, type: null), Today));

        Assert.Equal("Marque si la observación es académica, de convivencia o ambas.", error.Message);
    }

    [Fact]
    public async Task Academic_observation_ignores_the_type()
    {
        var (school, service) = await CreateAsync();

        var id = await service.CreateAsync(Teacher(school.T2), Request(school.Ana, academic: true, coexistence: false, type: "II"), Today);

        var observation = await school.Db.Observations.SingleAsync(o => o.ObservationId == id);
        Assert.Null(observation.CoexistenceType);
    }

    [Fact]
    public async Task Future_date_is_rejected()
    {
        var (school, service) = await CreateAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Teacher(school.T2), Request(school.Ana, date: Today.AddDays(1)), Today));

        Assert.Equal("La fecha de la observación no puede ser futura.", error.Message);
    }

    [Fact]
    public async Task Create_records_the_step_with_user_and_kind()
    {
        var (school, service) = await CreateAsync();

        var id = await service.CreateAsync(Teacher(school.T2), Request(school.Ana, academic: true, type: "ii"), Today);

        var detail = await service.GetAsync(Teacher(school.T2), id);
        Assert.Equal("II", detail.CoexistenceType);
        var step = Assert.Single(detail.Events);
        Assert.Equal(ObservationActions.Create, step.Action);
        Assert.Equal("Académica y Convivencia Tipo II", step.Detail);
        Assert.Equal("Tania Dos", step.User);
    }

    [Fact]
    public async Task Teacher_cannot_record_on_students_outside_their_classrooms()
    {
        var (school, service) = await CreateAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateAsync(Teacher(school.T2), Request(school.Dani), Today));
    }

    [Fact]
    public async Task Classroom_director_can_record_on_their_students()
    {
        var (school, service) = await CreateAsync();
        var director = TestData.AddUser(school.Db, "Diana", "Directora");
        school.Db.Teachers.Add(new Teacher { TeacherId = director.Id, Specialty = "Sociales" });
        school.C1001.DirectorTeacherId = director.Id;
        await school.Db.SaveChangesAsync();

        var id = await service.CreateAsync(Teacher(director), Request(school.Beto), Today);

        Assert.True(id > 0);
    }

    [Fact]
    public async Task Teacher_sees_every_observation_of_their_students_and_coordination_sees_all()
    {
        var (school, service) = await CreateAsync();
        await service.CreateAsync(Teacher(school.T2), Request(school.Ana), Today);
        await service.CreateAsync(Teacher(school.T1), Request(school.Dani), Today);

        var t1 = await service.ListAsync(Teacher(school.T1), new ObservationFilter());
        var t2 = await service.ListAsync(Teacher(school.T2), new ObservationFilter());
        var coordination = await service.ListAsync(Coordination(school), new ObservationFilter());

        Assert.Equal(2, t1.Count);
        Assert.Equal("Ruiz, Ana", Assert.Single(t2).StudentName);
        Assert.Equal(2, coordination.Count);
    }

    [Fact]
    public async Task Teacher_cannot_open_observations_of_other_classrooms()
    {
        var (school, service) = await CreateAsync();
        var id = await service.CreateAsync(Teacher(school.T1), Request(school.Dani), Today);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAsync(Teacher(school.T2), id));
    }

    [Fact]
    public async Task List_filters_by_kind_and_type()
    {
        var (school, service) = await CreateAsync();
        var coordination = Coordination(school);
        await service.CreateAsync(coordination, Request(school.Ana, academic: true, coexistence: false, type: null), Today);
        await service.CreateAsync(coordination, Request(school.Beto, type: CoexistenceTypes.TypeI), Today);
        await service.CreateAsync(coordination, Request(school.Caro, type: CoexistenceTypes.TypeIII), Today);

        var academic = await service.ListAsync(coordination, new ObservationFilter(Kind: "academic"));
        var coexistence = await service.ListAsync(coordination, new ObservationFilter(Kind: "COEXISTENCE"));
        var typeIii = await service.ListAsync(coordination, new ObservationFilter(CoexistenceType: "III"));

        Assert.Equal("Ruiz, Ana", Assert.Single(academic).StudentName);
        Assert.Equal(2, coexistence.Count);
        Assert.Equal("Lima, Caro", Assert.Single(typeIii).StudentName);
    }

    [Fact]
    public async Task Only_the_author_or_coordination_can_edit_and_the_change_is_traced()
    {
        var (school, service) = await CreateAsync();
        var id = await service.CreateAsync(Teacher(school.T2), Request(school.Ana), Today);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateAsync(Teacher(school.T1), id, Request(school.Ana, type: "II"), Today));

        await service.UpdateAsync(Teacher(school.T2), id, Request(school.Ana, type: "II"), Today);

        var detail = await service.GetAsync(Coordination(school), id);
        Assert.Equal("II", detail.CoexistenceType);
        Assert.Equal(
            "Tipo: Convivencia Tipo I → Convivencia Tipo II",
            detail.Events.Single(e => e.Action == ObservationActions.Update).Detail);
        Assert.False((await service.GetAsync(Teacher(school.T1), id)).CanEdit);
    }

    [Fact]
    public async Task Edit_without_changes_records_nothing()
    {
        var (school, service) = await CreateAsync();
        var id = await service.CreateAsync(Teacher(school.T2), Request(school.Ana), Today);

        await service.UpdateAsync(Teacher(school.T2), id, Request(school.Ana), Today);

        Assert.Equal(1, await school.Db.ObservationEvents.CountAsync());
    }

    [Fact]
    public async Task Statement_and_commitments_are_traced_step_by_step()
    {
        var (school, service) = await CreateAsync();
        var teacher = Teacher(school.T2);
        var id = await service.CreateAsync(teacher, Request(school.Ana), Today);

        await service.SaveStatementAsync(teacher, id, new SaveStatementRequest("  Dice que no se dio cuenta.  "));
        var commitmentId = await service.AddCommitmentAsync(teacher, id, new AddCommitmentRequest("Pedirá la palabra.", Today.AddDays(7)));
        await service.SetCommitmentStatusAsync(Coordination(school), commitmentId, new SetCommitmentStatusRequest("met", null));

        var detail = await service.GetAsync(teacher, id);
        Assert.Equal("Dice que no se dio cuenta.", detail.Statement);
        Assert.Equal(CommitmentStatuses.Met, Assert.Single(detail.Commitments).Status);
        Assert.Equal(
            [ObservationActions.Create, ObservationActions.Statement, ObservationActions.CommitmentAdded, ObservationActions.CommitmentStatus],
            detail.Events.Select(e => e.Action));
        Assert.Equal("Pedirá la palabra.: Pendiente → Cumplió", detail.Events[^1].Detail);
        Assert.Equal("Marta Rojas", detail.Events[^1].User);
    }

    [Fact]
    public async Task Observation_status_requires_a_note()
    {
        var (school, service) = await CreateAsync();
        var teacher = Teacher(school.T2);
        var id = await service.CreateAsync(teacher, Request(school.Ana), Today);
        var commitmentId = await service.AddCommitmentAsync(teacher, id, new AddCommitmentRequest("Traerá el taller.", null));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SetCommitmentStatusAsync(teacher, commitmentId, new SetCommitmentStatusRequest(CommitmentStatuses.Observation, " ")));

        Assert.Equal("Escriba la observación del compromiso.", error.Message);
    }

    [Fact]
    public async Task Scanned_statement_is_stored_as_document_and_traced()
    {
        var (school, service) = await CreateAsync();
        var teacher = Teacher(school.T2);
        var id = await service.CreateAsync(teacher, Request(school.Ana), Today);

        var file = await service.UploadStatementFileAsync(teacher, id, FakeDocumentStorage.Pdf("descargos.pdf"), Now);

        var detail = await service.GetAsync(teacher, id);
        Assert.Equal(file.DocumentId, detail.StatementFile?.DocumentId);
        Assert.Equal(ObservationActions.StatementFile, detail.Events[^1].Action);

        await service.RemoveStatementFileAsync(teacher, id, Now.AddMinutes(5));

        detail = await service.GetAsync(teacher, id);
        Assert.Null(detail.StatementFile);
        Assert.Equal(ObservationActions.StatementFileRemoved, detail.Events[^1].Action);
    }

    [Fact]
    public async Task Student_search_is_limited_to_the_teacher_scope()
    {
        var (school, service) = await CreateAsync();

        var t2 = await service.SearchStudentsAsync(Teacher(school.T2), null, null);
        var outsider = await service.SearchStudentsAsync(Teacher(TestData.AddUser(school.Db, "Otro", "Docente")), null, null);

        Assert.Equal(["Lima, Caro", "Paz, Beto", "Ruiz, Ana"], t2.Select(s => s.StudentName));
        Assert.Empty(outsider);
    }
}
