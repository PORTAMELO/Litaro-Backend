using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class TeacherAvailabilityEndpoints
    {
        public static void MapTeacherAvailabilityEndpoints(this WebApplication app)
        {
            app.MapGet("/teacher-availabilities", async (HttpRequest request, TeacherAvailabilityService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("TeacherAvailability", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/teacher-availabilities/{id:int}", async (int id, TeacherAvailabilityService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var availability = await svc.GetByIdAsync(id);

                return availability is not null
                    ? Results.Ok(availability)
                    : Results.NotFound($"No existe una disponibilidad con id {id}.");
            });

            app.MapPost("/teacher-availabilities", async (TeacherAvailabilityService.SaveAvailabilityRequest request, TeacherAvailabilityService svc) =>
            {
                try
                {
                    return Results.Ok(await svc.CreateAsync(request));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapPut("/teacher-availabilities/{id:int}", async (int id, TeacherAvailabilityService.SaveAvailabilityRequest request, TeacherAvailabilityService svc) =>
            {
                try
                {
                    var availability = await svc.UpdateAsync(id, request);
                    return availability is not null
                        ? Results.Ok(availability)
                        : Results.NotFound($"No existe una disponibilidad con id {id}.");
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapDelete("/teacher-availabilities/{id:int}", async (int id, TeacherAvailabilityService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound($"No existe una disponibilidad con id {id}."));
        }
    }
}
