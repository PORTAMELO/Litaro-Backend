using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class StudentMovementEndpoints
{
    public record MoveRequest(int EnrollmentId, int ToClassroomId, string? Note);
    public record WithdrawRequest(int EnrollmentId, string? Reason, string? Note);

    public static void MapStudentMovementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/student-movements");

        group.MapGet("/options", async (StudentMovementService svc) =>
                Results.Ok(await svc.GetOptionsAsync()))
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Read);

        group.MapGet("/students", async (string? q, StudentMovementService svc) =>
                Results.Ok(await svc.SearchAsync(q)))
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Read);

        group.MapGet("/students/{enrollmentId:int}", async (int enrollmentId, StudentMovementService svc) =>
                await svc.GetStudentAsync(enrollmentId) is { } student
                    ? Results.Ok(student)
                    : Results.NotFound())
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Read);

        group.MapGet("/students/{enrollmentId:int}/events", async (int enrollmentId, StudentMovementService svc) =>
                Results.Ok(await svc.GetEventsAsync(enrollmentId)))
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Read);

        group.MapGet("/events/{eventId:int}/certificate", async (int eventId, StudentMovementService svc) =>
                await svc.GetCertificateAsync(eventId) is { } certificate
                    ? Results.Ok(certificate)
                    : Results.NotFound())
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Read);

        group.MapPost("/classroom", (MoveRequest request, ClaimsPrincipal user, StudentMovementService svc) =>
                RunAsync(user, userId => svc.ChangeClassroomAsync(request.EnrollmentId, request.ToClassroomId, request.Note, userId)))
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Update);

        group.MapPost("/campus", (MoveRequest request, ClaimsPrincipal user, StudentMovementService svc) =>
                RunAsync(user, userId => svc.TransferCampusAsync(request.EnrollmentId, request.ToClassroomId, request.Note, userId)))
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Update);

        group.MapPost("/withdrawal", (WithdrawRequest request, ClaimsPrincipal user, StudentMovementService svc) =>
                RunAsync(user, userId => svc.WithdrawAsync(request.EnrollmentId, request.Reason, request.Note, userId)))
            .RequirePermission(ProcessKeys.StudentMovement, PermissionFlags.Update);
    }

    private static async Task<IResult> RunAsync(ClaimsPrincipal user, Func<int, Task<int>> action)
    {
        if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Results.Unauthorized();

        try
        {
            return Results.Ok(new { enrollmentEventId = await action(userId) });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
