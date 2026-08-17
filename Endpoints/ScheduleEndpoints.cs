using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class ScheduleEndpoints
    {
        public static void MapScheduleEndpoints(this WebApplication app)
        {
            app.MapGet("/schedules", async (ScheduleService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/schedules/{id}", async (int id, ScheduleService svc) =>
                await svc.GetByIdAsync(id) is Schedule s
                    ? Results.Ok(s)
                    : Results.NotFound());

            app.MapPost("/schedules", async (Schedule schedule, ScheduleService svc) =>
            {
                var created = await svc.CreateAsync(schedule);
                return Results.Created($"/schedules/{created.ScheduleId}", created);
            });

            app.MapPut("/schedules/{id}", async (int id, Schedule schedule, ScheduleService svc) =>
                await svc.UpdateAsync(id, schedule)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/schedules/{id}", async (int id, ScheduleService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }

    }
}
