using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class StudentLogEndpoints
    {
        public static void MapStudentLogEndpoints(this WebApplication app)
        {
            app.MapGet("/student-logs", async ([FromServices] StudentLogService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/student-logs/{id}", async (int id, [FromServices] StudentLogService svc) =>
                await svc.GetByIdAsync(id) is StudentLog sl
                    ? Results.Ok(sl)
                    : Results.NotFound());

            app.MapPost("/student-logs", async (StudentLog log, [FromServices] StudentLogService svc) =>
            {
                var created = await svc.CreateAsync(log);
                return Results.Created($"/student-logs/{created.LogId}", created);
            });

            app.MapPut("/student-logs/{id}", async (int id, StudentLog log, [FromServices] StudentLogService svc) =>
                await svc.UpdateAsync(id, log)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/student-logs/{id}", async (int id, [FromServices] StudentLogService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}
