using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class AcademicAssignmentEndpoints
    {
        public static void MapAcademicAssignmentEndpoints(this WebApplication app)
        {
            app.MapGet("/academic-assignments", async (HttpRequest request, AcademicAssignmentService svc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                return Results.Ok(await svc.GetAllAsync(filters));
            });

            app.MapGet("/academic-assignments/{id:int}", async (int id, AcademicAssignmentService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var assignment = await svc.GetByIdAsync(id);

                return assignment is not null
                    ? Results.Ok(assignment)
                    : Results.NotFound($"No existe una asignación activa con id {id}.");
            });
        }

    }
}
