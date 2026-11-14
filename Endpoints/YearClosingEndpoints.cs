using System.Security.Claims;
using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class YearClosingEndpoints
{
    public static void MapYearClosingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/year-closing");

        group.MapGet("/options", async (short? yearId, YearClosingService svc) =>
                Results.Ok(await svc.GetOptionsAsync(yearId)))
            .RequirePermission(ProcessKeys.YearClosing, PermissionFlags.Read);

        group.MapGet("/preview", (short yearId, int classroomId, YearClosingService svc) =>
                Guard(async () => Results.Ok(await svc.PreviewAsync(yearId, classroomId))))
            .RequirePermission(ProcessKeys.YearClosing, PermissionFlags.Read);

        group.MapPost("/close", (CloseRequest request, ClaimsPrincipal user, YearClosingService svc) =>
                Guard(async () =>
                    int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                        ? Results.Ok(await svc.CloseAsync(request, userId))
                        : Results.Unauthorized()))
            .RequirePermission(ProcessKeys.YearClosing, PermissionFlags.Update);

        group.MapGet("/settings", async (PromotionSettingsService svc) =>
                Results.Ok(await svc.GetAsync()))
            .RequirePermission(ProcessKeys.YearClosing, PermissionFlags.Read);

        group.MapPut("/settings", (PromotionSettings request, PromotionSettingsService svc) =>
                Guard(async () => Results.Ok(await svc.SaveAsync(request))))
            .RequirePermission(ProcessKeys.YearClosing, PermissionFlags.Update);
    }

    private static async Task<IResult> Guard(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
