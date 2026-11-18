using Litaro.Data;
using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Litaro.Tests;

public class AuditServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LogAccess_copies_request_context()
    {
        var db = TestDb.Create();
        var ctx = new AuditContext();
        ctx.SetRequest("POST /auth/login", "10.0.0.1", "UA");

        await new AuditService(db, ctx).LogAccessAsync("LOGIN_OK", 5, "Ana Pérez", null, Now);

        var row = await db.AuditLogs.SingleAsync();
        Assert.Equal("LOGIN_OK", row.EventType);
        Assert.Equal("Auth", row.TableName);
        Assert.Equal(5, row.UserId);
        Assert.Equal("Ana Pérez", row.UserName);
        Assert.Equal("USER", row.ActorKind);
        Assert.Equal("10.0.0.1", row.IpAddress);
        Assert.Equal("UA", row.UserAgent);
        Assert.Equal("POST /auth/login", row.Source);
        Assert.Equal(ctx.OperationId, row.OperationId);
        Assert.Null(row.Detail);
    }

    [Fact]
    public async Task LogAccess_failure_stores_reason_in_detail()
    {
        var db = TestDb.Create();
        var ctx = new AuditContext();
        ctx.SetRequest("POST /auth/login", "10.0.0.1", "UA");

        await new AuditService(db, ctx).LogAccessAsync("LOGIN_FAIL", null, new string('x', 150), "USER_NOT_FOUND", Now);

        var row = await db.AuditLogs.SingleAsync();
        Assert.Equal(100, row.UserName!.Length);
        Assert.Equal("ANONYMOUS", row.ActorKind);
        Assert.Null(row.UserId);
        Assert.Equal("{\"reason\":\"USER_NOT_FOUND\"}", row.Detail);
    }

    [Theory]
    [InlineData("USER_NULL", false, "USER_NOT_FOUND")]
    [InlineData("USER_INACTIVE", false, "USER_INACTIVE")]
    [InlineData("PASSWORD_CHECK_FAILED", false, "WRONG_PASSWORD")]
    [InlineData("PASSWORD_CHECK_FAILED", true, "LOCKED_OUT")]
    [InlineData("ACTIVATION_REQUIRED", false, "ACTIVATION_REQUIRED")]
    [InlineData("MUST_CHANGE_PASSWORD", false, "MUST_CHANGE_PASSWORD")]
    public void Reasons_map_login_steps(string step, bool lockedOut, string expected)
    {
        Assert.Equal(expected, LoginAuditReasons.For(step, lockedOut));
    }

    [Fact]
    public async Task Activation_attributes_changes_to_the_target_user()
    {
        var db = TestDb.Create();
        var user = TestData.AddUser(db, "Sara", "Vélez");
        user.UserName = user.DocumentNumber;
        db.Students.Add(new Student
        {
            StudentId = user.Id,
            StudentCode = "S1",
            Gender = 'F',
            BirthDate = new DateTime(2015, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        db.SaveChanges();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddHttpContextAccessor();
        services.AddAuthentication();
        services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AppDbContext>().AddSignInManager();
        var provider = services.BuildServiceProvider();

        var audit = new AuditContext();
        var accounts = new AccountService(
            db,
            provider.GetRequiredService<UserManager<User>>(),
            provider.GetRequiredService<SignInManager<User>>(),
            audit);

        await accounts.ActivateAsync(new ActivateAccountRequest(user.DocumentNumber, new DateOnly(2015, 3, 1), "Abcdefg1!"));

        Assert.Equal(user.Id, audit.UserId);
        Assert.Equal("USER", audit.ActorKind);
    }
}
