using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class EnrollmentProcessEndpoints
{
    public record RespondRequest(bool Confirm, string? Note);
    public record DecisionRequest(string? Decision, string? Note);

    public static void MapEnrollmentProcessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/enrollment-process");

        group.MapGet("/options", async (EnrollmentPeriodService periods) =>
                Results.Ok(await periods.GetOptionsAsync()))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapGet("/periods/{yearId}", async (short yearId, EnrollmentPeriodService periods) =>
                await periods.GetAsync(yearId) is { } period
                    ? Results.Ok(period)
                    : Results.NoContent())
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapPut("/periods/{yearId}", async (short yearId, SaveEnrollmentPeriodRequest request, EnrollmentPeriodService periods) =>
            {
                try
                {
                    return Results.Ok(await periods.SaveAsync(yearId, request));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Update);

        group.MapGet("/pre-enrollments", async (short yearId, PreEnrollmentService svc) =>
                Results.Ok(await svc.ListAsync(yearId)))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapPost("/pre-enrollments/generate", async (short yearId, PreEnrollmentService svc) =>
            {
                try
                {
                    return Results.Ok(await svc.GenerateAsync(yearId));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Create);

        group.MapPost("/pre-enrollments/restore", async (short yearId, ClaimsPrincipal user, PreEnrollmentService svc) =>
            {
                if (!int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                    return Results.Unauthorized();

                try
                {
                    return Results.Ok(await svc.RestoreLostAsync(yearId, userId));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Update);

        group.MapPost("/pre-enrollments/{id}/respond", async (int id, RespondRequest request, PreEnrollmentService svc) =>
            {
                try
                {
                    await svc.RespondAsync(id, request.Confirm, request.Note);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Update);

        group.MapGet("/admissions", async (short yearId, AdmissionService svc) =>
                Results.Ok(await svc.ListAsync(yearId)))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapGet("/admissions/{id}/events", async (int id, AdmissionService svc) =>
                Results.Ok(await svc.GetEventsAsync(id)))
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Read);

        group.MapPost("/admissions", async (CreateAdmissionRequest request, ClaimsPrincipal user, AdmissionService svc) =>
            {
                if (CurrentUserId(user) is not { } userId)
                    return Results.Unauthorized();

                try
                {
                    var id = await svc.CreateAsync(request, userId);
                    return Results.Ok(new { admissionRequestId = id });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.EnrollmentProcess, PermissionFlags.Create);

        group.MapPost("/admissions/{id}/decision", async (int id, DecisionRequest request, ClaimsPrincipal user, AdmissionService svc) =>
            {
                if (CurrentUserId(user) is not { } userId)
                    return Results.Unauthorized();

                try
                {
                    await svc.DecideAsync(id, request.Decision, request.Note, userId);
                    return Results.NoContent();
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequirePermission(ProcessKeys.AdmissionDecision, PermissionFlags.Update);
    }

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
