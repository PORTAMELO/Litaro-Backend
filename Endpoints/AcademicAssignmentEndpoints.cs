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

            app.MapPost("/academic-assignments", async (AcademicAssignmentService.CreateAssignmentRequest request, AcademicAssignmentService svc) =>
            {
                try
                {
                    return Results.Ok(await svc.CreateAsync(request));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            });

            app.MapPatch("/academic-assignments/{id:int}/teacher", async (int id, AcademicAssignmentService.ChangeTeacherRequest request, AcademicAssignmentService svc) =>
            {
                try
                {
                    var assignment = await svc.ChangeTeacherAsync(id, request.TeacherId);
                    return assignment is not null
                        ? Results.Ok(assignment)
                        : Results.NotFound($"No existe una asignación activa con id {id}.");
                }
                catch (ScheduleConflictException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            });
        }

    }
}
