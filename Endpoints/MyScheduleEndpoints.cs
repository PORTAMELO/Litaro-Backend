using System.Security.Claims;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class MyScheduleEndpoints
    {
        public static void MapMyScheduleEndpoints(this WebApplication app)
        {
            app.MapGet("/me/schedule", async (HttpContext ctx, short? yearId, ScheduleService svc) =>
            {
                if (!int.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                    return Results.Unauthorized();

                return Results.Ok(await svc.GetForUserAsync(userId, yearId));
            });
        }
    }
}
