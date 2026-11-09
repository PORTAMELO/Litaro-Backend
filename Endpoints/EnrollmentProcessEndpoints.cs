using System.Security.Claims;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class EnrollmentProcessEndpoints
{
    private static readonly string[] Staff = ["Administrador", "Secretaría", "Secretaria"];
    private static readonly string[] StaffAndRector = ["Administrador", "Secretaría", "Secretaria", "Rector"];
    private static readonly string[] RectorOnly = ["Rector"];

    public record RespondRequest(bool Confirm, string? Note);
    public record DecisionRequest(string? Decision, string? Note);
    public record ResolveAlertsRequest(List<int>? Ids, string? Note);

    public static void MapEnrollmentProcessEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/enrollment-process");

        group.MapGet("/options", async (EnrollmentPeriodService periods) =>
                Results.Ok(await periods.GetOptionsAsync()))
            .RequireAuthorization(policy => policy.RequireRole(StaffAndRector));

        group.MapGet("/periods/{yearId}", async (short yearId, EnrollmentPeriodService periods) =>
                await periods.GetAsync(yearId) is { } period
                    ? Results.Ok(period)
                    : Results.NoContent())
            .RequireAuthorization(policy => policy.RequireRole(StaffAndRector));

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
            .RequireAuthorization(policy => policy.RequireRole(Staff));

        group.MapGet("/pre-enrollments", async (short yearId, PreEnrollmentService svc) =>
                Results.Ok(await svc.ListAsync(yearId)))
            .RequireAuthorization(policy => policy.RequireRole(StaffAndRector));

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
            .RequireAuthorization(policy => policy.RequireRole(Staff));

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
            .RequireAuthorization(policy => policy.RequireRole(Staff));

        group.MapGet("/admissions", async (short yearId, AdmissionService svc) =>
                Results.Ok(await svc.ListAsync(yearId)))
            .RequireAuthorization(policy => policy.RequireRole(StaffAndRector));

        group.MapGet("/admissions/{id}/events", async (int id, AdmissionService svc) =>
                Results.Ok(await svc.GetEventsAsync(id)))
            .RequireAuthorization(policy => policy.RequireRole(StaffAndRector));

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
            .RequireAuthorization(policy => policy.RequireRole(Staff));

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
            .RequireAuthorization(policy => policy.RequireRole(RectorOnly));

        group.MapGet("/simat-alerts", async (string? status, SimatAlertService svc) =>
                Results.Ok(await svc.ListAsync(status)))
            .RequireAuthorization(policy => policy.RequireRole(Staff));

        group.MapPost("/simat-alerts/resolve", async (ResolveAlertsRequest request, ClaimsPrincipal user, SimatAlertService svc) =>
            {
                if (CurrentUserId(user) is not { } userId)
                    return Results.Unauthorized();

                try
                {
                    var resolved = await svc.ResolveAsync(request.Ids ?? [], request.Note, userId);
                    return Results.Ok(new { resolved });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            })
            .RequireAuthorization(policy => policy.RequireRole(Staff));
    }

    private static int? CurrentUserId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}