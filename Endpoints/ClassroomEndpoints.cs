using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class ClassroomEndpoints
    {
        public static void MapClassroomEndpoints(this WebApplication app)
        {
            app.MapGet("/classrooms", async (HttpRequest request, ClassroomService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Classroom", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/classrooms/{id:int}", async (int id, ClassroomService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var classroom = await svc.GetByIdAsync(id);

                return classroom is not null
                    ? Results.Ok(classroom)
                    : Results.NotFound($"No existe un salón activo con id {id}.");
            });

            app.MapPatch("/classrooms/{id:int}/schedule-settings", async (int id, ClassroomService.ScheduleSettingsRequest request, ClassroomService svc) =>
            {
                try
                {
                    var classroom = await svc.UpdateScheduleSettingsAsync(id, request);
                    return classroom is not null
                        ? Results.Ok(classroom)
                        : Results.NotFound($"No existe un salón activo con id {id}.");
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });
        }

    }
}
