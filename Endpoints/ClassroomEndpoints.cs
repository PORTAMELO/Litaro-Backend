using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class ClassroomEndpoints
    {
        public static void MapClassroomEndpoints(this WebApplication app)
        {
            app.MapGet("/classrooms", async ([FromServices] ClassroomService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/classrooms/{id}", async (int id, [FromServices] ClassroomService svc) =>
                await svc.GetByIdAsync(id) is Classroom c
                    ? Results.Ok(c)
                    : Results.NotFound());

            app.MapPost("/classrooms", async (Classroom classroom, [FromServices] ClassroomService svc) =>
            {
                var created = await svc.CreateAsync(classroom);
                return Results.Created($"/classrooms/{created.ClassroomId}", created);
            });

            app.MapPut("/classrooms/{id}", async (int id, Classroom classroom, [FromServices] ClassroomService svc) =>
                await svc.UpdateAsync(id, classroom)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/classrooms/{id}/close", async (int id, [FromServices] ClassroomService svc) =>
                await svc.DeactivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPatch("/classrooms/{id}/activate", async (int id, [FromServices] ClassroomService svc) =>
                await svc.ActivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }

    }
}
