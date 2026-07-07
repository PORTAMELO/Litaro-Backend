using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class AcademicAssignmentEndpoints
    {
        public static void MapAcademicAssignmentEndpoints(this WebApplication app)
        {
            app.MapGet("/academic-assignments", async (AcademicAssignmentService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/academic-assignments/{id}", async (int id, [FromServices] AcademicAssignmentService svc) =>
                await svc.GetByIdAsync(id) is AcademicAssignment a
                    ? Results.Ok(a)
                    : Results.NotFound());

            app.MapPost("/academic-assignments", async (AcademicAssignment assignment, AcademicAssignmentService svc) =>
            {
                var created = await svc.CreateAsync(assignment);
                return Results.Created($"/academic-assignments/{created.AssignmentId}", created);
            });

            app.MapPut("/academic-assignments/{id}", async (int id, AcademicAssignment assignment, AcademicAssignmentService svc) =>
                await svc.UpdateAsync(id, assignment)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/academic-assignments/{id}/close", async (int id, AcademicAssignmentService svc) =>
                await svc.DeactivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapPatch("/academic-assignments/{id}/activate", async (int id, AcademicAssignmentService svc) =>
                await svc.ActivateAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }

    }
}
