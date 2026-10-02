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

            app.MapGet("/schedules/view", async (HttpRequest request, ScheduleService svc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                return Results.Ok(await svc.GetViewAsync(filters));
            });

            app.MapGet("/schedules/progress", async (int classroomId, short yearId, ScheduleService svc) =>
            {
                try
                {
                    return Results.Ok(await svc.GetProgressAsync(classroomId, yearId));
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            });

            app.MapGet("/schedules/parent-attention", async (short yearId, int? campusId, ScheduleService svc) =>
                Results.Ok(await svc.GetParentAttentionBoardAsync(yearId, campusId)));

            app.MapGet("/schedules/{id:int}", async (int id, ScheduleService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var schedule = await svc.GetViewByIdAsync(id);

                return schedule is not null
                    ? Results.Ok(schedule)
                    : Results.NotFound($"No existe un horario con id {id}.");
            });

            app.MapPost("/schedules", (ScheduleService.SaveScheduleRequest request, ScheduleService svc) =>
                SaveAsync(svc, null, request));

            app.MapPut("/schedules/{id:int}", (int id, ScheduleService.SaveScheduleRequest request, ScheduleService svc) =>
                SaveAsync(svc, id, request));

            app.MapDelete("/schedules/{id:int}", async (int id, ScheduleService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound($"No existe un horario con id {id}."));
        }

        private static async Task<IResult> SaveAsync(ScheduleService svc, int? id, ScheduleService.SaveScheduleRequest request)
        {
            try
            {
                var result = await svc.SaveAsync(id, request);
                return Results.Ok(new { schedule = result.Schedule, warnings = result.Warnings });
            }
            catch (ScheduleConflictException ex)
            {
                return Results.Conflict(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }
    }
}
