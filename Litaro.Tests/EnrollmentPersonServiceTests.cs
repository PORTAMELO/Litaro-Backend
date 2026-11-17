using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Tests;

public class EnrollmentPersonServiceTests
{
    private sealed record Setup(AppDbContext Db, EnrollmentPersonService Service, Campus Campus, IdentityRole<int>? StudentRole, IdentityRole<int>? ParentRole);

    private static Setup Create(bool studentRole = true, string? parentRole = ParentService.RoleName)
    {
        var db = TestDb.Create();
        var campus = EnrollmentTestData.AddCampus(db);
        var student = studentRole ? TestData.AddRole(db, StudentService.RoleName) : null;
        var parent = parentRole is null ? null : TestData.AddRole(db, parentRole);
        return new Setup(db, new EnrollmentPersonService(db), campus, student, parent);
    }

    private static User AddUser(AppDbContext db, string number, string documentType = "CC")
    {
        var user = TestData.AddUser(db, "Previo", "Usuario");
        user.DocumentNumber = number;
        user.DocumentType = documentType;
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task New_student_gets_user_code_role_and_no_password()
    {
        var setup = Create();
        var data = EnrollmentTestData.Student(setup.Campus.CampusId) with { Email = "Sara@Correo.co" };

        await setup.Service.EnsureCanSaveAsync(data, EnrollmentTestData.Guardian());
        var student = await setup.Service.UpsertStudentAsync(data, EnrollmentTestData.C600());

        var user = await setup.Db.Users.SingleAsync(u => u.DocumentNumber == EnrollmentTestData.StudentNumber);

        Assert.Equal(user.Id, student.StudentId);
        Assert.Equal("1061000001", user.UserName);
        Assert.Equal("1061000001", user.NormalizedUserName);
        Assert.Equal("SARA@CORREO.CO", user.NormalizedEmail);
        Assert.Null(user.PasswordHash);
        Assert.True(user.MustChangePassword);
        Assert.True(user.LockoutEnabled);
        Assert.True(user.Active);
        Assert.False(string.IsNullOrEmpty(user.SecurityStamp));
        Assert.Equal(setup.Campus.CampusId, user.CampusId);
        Assert.Equal("1061000001", student.StudentCode);
        Assert.Equal("00", student.EthnicGroup);
        Assert.Equal("SURA", student.Eps);
        Assert.Equal('F', student.Gender);
        Assert.Equal(new DateTime(2015, 3, 1, 0, 0, 0, DateTimeKind.Utc), student.BirthDate);
        Assert.Equal(DateTimeKind.Utc, student.BirthDate.Kind);
        Assert.True(await setup.Db.UserRoles.AnyAsync(r => r.UserId == user.Id && r.RoleId == setup.StudentRole!.Id));
    }

    [Fact]
    public async Task New_student_uses_the_next_free_username_and_code()
    {
        var setup = Create();
        var other = AddUser(setup.Db, "999");
        other.UserName = "1061000001";
        other.NormalizedUserName = "1061000001";
        setup.Db.Students.Add(new Student { StudentId = other.Id, StudentCode = "1061000001", Gender = 'M' });
        await setup.Db.SaveChangesAsync();

        var student = await setup.Service.UpsertStudentAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.C600());
        var user = await setup.Db.Users.SingleAsync(u => u.Id == student.StudentId);

        Assert.Equal("TI1061000001", user.UserName);
        Assert.Equal("TI1061000001", student.StudentCode);
    }

    [Fact]
    public async Task Existing_student_is_updated_not_duplicated()
    {
        var setup = Create();
        var existing = AddUser(setup.Db, EnrollmentTestData.StudentNumber, "RC");
        existing.Email = "viejo@correo.co";
        setup.Db.Students.Add(new Student { StudentId = existing.Id, StudentCode = "INPS-9", Gender = 'F', Eps = "SALUD VIDA", Active = false });
        await setup.Db.SaveChangesAsync();

        var data = EnrollmentTestData.Student(setup.Campus.CampusId) with { FirstName = "Sarita", Eps = "NUEVA EPS" };

        await setup.Service.UpsertStudentAsync(data, EnrollmentTestData.C600());
        await setup.Service.UpsertStudentAsync(data, EnrollmentTestData.C600());

        var users = await setup.Db.Users.Where(u => u.DocumentNumber == EnrollmentTestData.StudentNumber).ToListAsync();
        var student = await setup.Db.Students.SingleAsync();

        Assert.Single(users);
        Assert.Equal("Sarita", users[0].FirstName);
        Assert.Equal("TI", users[0].DocumentType);
        Assert.Equal("viejo@correo.co", users[0].Email);
        Assert.Equal("NUEVA EPS", student.Eps);
        Assert.Equal("INPS-9", student.StudentCode);
        Assert.True(student.Active);
        Assert.Equal(1, await setup.Db.UserRoles.CountAsync(r => r.UserId == existing.Id));
    }

    [Fact]
    public async Task Guardian_is_created_linked_and_marked_primary()
    {
        var setup = Create();
        var student = await setup.Service.UpsertStudentAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.C600());

        var parent = await setup.Service.UpsertGuardianAsync(EnrollmentTestData.Guardian(), setup.Campus.CampusId, student.StudentId);

        var user = await setup.Db.Users.SingleAsync(u => u.Id == parent.ParentId);
        var link = await setup.Db.ParentStudents.SingleAsync();

        Assert.Equal(EnrollmentTestData.GuardianNumber, user.DocumentNumber);
        Assert.Equal("3001112233", user.PhoneNumber);
        Assert.Equal(EnrollmentTestData.GuardianEmail, user.Email);
        Assert.Null(user.PasswordHash);
        Assert.Equal("Calle 1 # 2-3", parent.Address);
        Assert.Equal("MOTHER", parent.Relationship);
        Assert.Equal(student.StudentId, link.StudentId);
        Assert.True(link.PrimaryContact);
        Assert.True(await setup.Db.UserRoles.AnyAsync(r => r.UserId == user.Id && r.RoleId == setup.ParentRole!.Id));
    }

    [Fact]
    public async Task Existing_guardian_gains_the_link_without_a_second_user()
    {
        var setup = Create();
        var guardianUser = AddUser(setup.Db, EnrollmentTestData.GuardianNumber);
        var sibling = AddUser(setup.Db, "1061000077", "TI");
        setup.Db.Students.Add(new Student { StudentId = sibling.Id, StudentCode = "S77", Gender = 'M' });
        setup.Db.Parents.Add(new Parent { ParentId = guardianUser.Id, Relationship = "FATHER", Active = false });
        setup.Db.ParentStudents.Add(new ParentStudent { ParentId = guardianUser.Id, StudentId = sibling.Id });
        await setup.Db.SaveChangesAsync();

        var student = await setup.Service.UpsertStudentAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.C600());
        await setup.Service.UpsertGuardianAsync(EnrollmentTestData.Guardian(), setup.Campus.CampusId, student.StudentId);
        await setup.Service.UpsertGuardianAsync(EnrollmentTestData.Guardian(), setup.Campus.CampusId, student.StudentId);

        var parent = await setup.Db.Parents.SingleAsync();

        Assert.Single(await setup.Db.Users.Where(u => u.DocumentNumber == EnrollmentTestData.GuardianNumber).ToListAsync());
        Assert.Equal(2, await setup.Db.ParentStudents.CountAsync(l => l.ParentId == guardianUser.Id));
        Assert.True(parent.Active);
        Assert.Equal("MOTHER", parent.Relationship);
    }

    [Fact]
    public async Task Guardian_role_falls_back_to_its_alias()
    {
        var setup = Create(parentRole: "Padre");
        var student = await setup.Service.UpsertStudentAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.C600());

        await setup.Service.EnsureCanSaveAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.Guardian());
        var parent = await setup.Service.UpsertGuardianAsync(EnrollmentTestData.Guardian(), setup.Campus.CampusId, student.StudentId);

        Assert.True(await setup.Db.UserRoles.AnyAsync(r => r.UserId == parent.ParentId && r.RoleId == setup.ParentRole!.Id));
    }

    [Fact]
    public async Task Missing_roles_block_before_any_write()
    {
        var withoutStudent = Create(studentRole: false);
        var withoutParent = Create(parentRole: null);

        var noStudent = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            withoutStudent.Service.EnsureCanSaveAsync(EnrollmentTestData.Student(1), EnrollmentTestData.Guardian()));
        var noParent = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            withoutParent.Service.EnsureCanSaveAsync(EnrollmentTestData.Student(1), EnrollmentTestData.Guardian()));

        Assert.Equal("No existe el rol de estudiante.", noStudent.Message);
        Assert.Equal("No existe el rol de acudiente.", noParent.Message);
        await withoutParent.Service.EnsureCanSaveAsync(EnrollmentTestData.Student(1), null);
    }

    [Fact]
    public async Task Duplicate_document_numbers_block_before_any_write()
    {
        var setup = Create();
        AddUser(setup.Db, EnrollmentTestData.GuardianNumber);
        AddUser(setup.Db, EnrollmentTestData.GuardianNumber);
        var usersBefore = await setup.Db.Users.CountAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.EnsureCanSaveAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.Guardian()));

        Assert.Equal("Hay más de un usuario con el documento 52000111. Resuelva el duplicado antes de continuar.", error.Message);
        Assert.Equal(usersBefore, await setup.Db.Users.CountAsync());
    }

    [Fact]
    public async Task Same_email_for_student_and_guardian_blocks_before_any_write()
    {
        var setup = Create();
        var student = EnrollmentTestData.Student(setup.Campus.CampusId) with { Email = EnrollmentTestData.GuardianEmail };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.EnsureCanSaveAsync(student, EnrollmentTestData.Guardian()));

        Assert.Equal("El estudiante y el acudiente no pueden tener el mismo correo.", error.Message);
    }

    [Fact]
    public async Task A_new_primary_guardian_replaces_the_previous_primary()
    {
        var setup = Create();
        var student = await setup.Service.UpsertStudentAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.C600());
        var first = await setup.Service.UpsertGuardianAsync(EnrollmentTestData.Guardian(), setup.Campus.CampusId, student.StudentId);
        var other = EnrollmentTestData.Guardian() with { DocumentNumber = "80000002", Email = "pedro@correo.co", Relationship = "FATHER" };

        var second = await setup.Service.UpsertGuardianAsync(other, setup.Campus.CampusId, student.StudentId);
        await setup.Service.UpsertGuardianAsync(other, setup.Campus.CampusId, student.StudentId);

        var links = await setup.Db.ParentStudents.AsNoTracking().ToListAsync();

        Assert.Equal(2, links.Count);
        Assert.False(links.Single(l => l.ParentId == first.ParentId).PrimaryContact);
        Assert.True(links.Single(l => l.ParentId == second.ParentId).PrimaryContact);
    }

    [Fact]
    public async Task Email_of_another_person_blocks_before_any_write()
    {
        var setup = Create();
        var other = AddUser(setup.Db, "888");
        other.Email = EnrollmentTestData.GuardianEmail;
        other.NormalizedEmail = EnrollmentTestData.GuardianEmail.ToUpperInvariant();
        await setup.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setup.Service.EnsureCanSaveAsync(EnrollmentTestData.Student(setup.Campus.CampusId), EnrollmentTestData.Guardian()));

        Assert.Equal("El correo luz@correo.co ya pertenece a otra persona.", error.Message);
    }
}
