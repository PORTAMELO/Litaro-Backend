using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints;

public static class SchoolEndpoints
{
    public static void MapSchoolEndpoints(this WebApplication app)
    {
        app.MapGet("/schools", async (SchoolService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        app.MapGet("/schools/{id}", async (int id, SchoolService svc) =>
            await svc.GetByIdAsync(id) is School s
                ? Results.Ok(s)
                : Results.NotFound());

        app.MapPost("/schools", async (School school, SchoolService svc) =>
        {
            var created = await svc.CreateAsync(school);
            return Results.Created($"/schools/{created.SchoolId}", created);
        });

        app.MapPut("/schools/{id}", async (int id, School school, SchoolService svc) =>
            await svc.UpdateAsync(id, school)
                ? Results.NoContent()
                : Results.NotFound());

        app.MapDelete("/schools/{id}/close", async (int id, SchoolService svc) =>
            await svc.DeactivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

        app.MapPatch(pattern: "/schools/{id}/activate", async (int id, SchoolService svc) =>
            await svc.ActivateAsync(id)
                ? Results.NoContent()
                : Results.NotFound());

    }
}