using Litaro.Data;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Tests;

public class AccountActivationTests
{
    private static readonly DateOnly Birth = new(2015, 3, 1);

    private static (AppDbContext Db, User Student, User Guardian) Create(bool guardianActive = true, bool linked = true)
    {
        var db = TestDb.Create();
        var student = TestData.AddUser(db, "Sara", "Vélez");
        var guardian = TestData.AddUser(db, "Luz", "Vélez");

        db.Students.Add(new Student
        {
            StudentId = student.Id,
            StudentCode = "S1",
            Gender = 'F',
            BirthDate = new DateTime(2015, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        db.Parents.Add(new Parent { ParentId = guardian.Id, Relationship = "MOTHER", Active = guardianActive });

        if (linked)
            db.ParentStudents.Add(new ParentStudent { ParentId = guardian.Id, StudentId = student.Id });

        db.SaveChanges();
        return (db, student, guardian);
    }

    [Fact]
    public async Task Student_matches_own_birth_date()
    {
        var (db, student, _) = Create();

        Assert.True(await AccountService.BirthDateMatchesAsync(db, student.Id, Birth));
        Assert.False(await AccountService.BirthDateMatchesAsync(db, student.Id, Birth.AddDays(1)));
    }

    [Fact]
    public async Task Guardian_matches_the_birth_date_of_a_linked_student_only()
    {
        var (db, _, guardian) = Create();

        Assert.True(await AccountService.BirthDateMatchesAsync(db, guardian.Id, Birth));
        Assert.False(await AccountService.BirthDateMatchesAsync(db, guardian.Id, new DateOnly(1985, 6, 1)));
    }

    [Fact]
    public async Task Inactive_guardian_or_unlinked_student_does_not_match()
    {
        var (inactiveDb, _, inactive) = Create(guardianActive: false);
        var (unlinkedDb, _, unlinked) = Create(linked: false);

        Assert.False(await AccountService.BirthDateMatchesAsync(inactiveDb, inactive.Id, Birth));
        Assert.False(await AccountService.BirthDateMatchesAsync(unlinkedDb, unlinked.Id, Birth));
    }
}
