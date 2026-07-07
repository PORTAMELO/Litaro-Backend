using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class SubjectEndpoints
    {
        public static void MapSubjectEndpoints(this WebApplication app)
        {
            app.MapGet("/subjects", async ([FromServices] SubjectService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/subjects/{id}", async (int id, [FromServices] SubjectService svc) =>
                await svc.GetByIdAsync(id) is Subject s
                    ? Results.Ok(s)
                    : Results.NotFound());

            app.MapPost("/subjects", async (Subject subject, [FromServices] SubjectService svc) =>
            {
                var created = await svc.CreateAsync(subject);
                return Results.Created($"/subjects/{created.SubjectId}", created);
            });

            app.MapPut("/subjects/{id}", async (int id, Subject subject, [FromServices] SubjectService svc) =>
                await svc.UpdateAsync(id, subject)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/subjects/{id}", async (int id, [FromServices] SubjectService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}
