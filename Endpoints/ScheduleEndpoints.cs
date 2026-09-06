using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class ScheduleEndpoints
    {
        public static void MapScheduleEndpoints(this WebApplication app)
        {
            app.MapGet("/schedules", async (HttpRequest request, ScheduleService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Schedule", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/schedules/{id:int}", async (int id, ScheduleService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var schedule = await svc.GetByIdAsync(id);

                return schedule is not null
                    ? Results.Ok(schedule)
                    : Results.NotFound($"No existe un horario con id {id}.");
            });
        }

    }
}
