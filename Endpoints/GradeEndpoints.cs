using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class GradeEndpoints
    {
        public static void MapGradeEndpoints(this WebApplication app)
        {
            app.MapGet("/grades", async ([FromServices] GradeService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/grades/{id}", async (int id, [FromServices] GradeService svc) =>
                await svc.GetByIdAsync(id) is Grade g
                    ? Results.Ok(g)
                    : Results.NotFound());

            app.MapPost("/grades", async (Grade grade, [FromServices] GradeService svc) =>
            {
                var created = await svc.CreateAsync(grade);
                return Results.Created($"/grades/{created.GradeId}", created);
            });

            app.MapPut("/grades/{id}", async (int id, Grade grade, [FromServices] GradeService svc) =>
                await svc.UpdateAsync(id, grade)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/grades/{id}", async (int id, [FromServices] GradeService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}
