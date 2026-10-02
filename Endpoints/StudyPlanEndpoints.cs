using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class StudyPlanEndpoints
    {
        public static void MapStudyPlanEndpoints(this WebApplication app)
        {
            app.MapGet("/study-plans", async (HttpRequest request, StudyPlanService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("StudyPlan", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/study-plans/{id:int}", async (int id, StudyPlanService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var plan = await svc.GetByIdAsync(id);

                return plan is not null
                    ? Results.Ok(plan)
                    : Results.NotFound($"No existe un plan de estudios con id {id}.");
            });

            app.MapPost("/study-plans", async (StudyPlanService.SaveStudyPlanRequest request, StudyPlanService svc) =>
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

            app.MapPut("/study-plans/{id:int}", async (int id, StudyPlanService.SaveStudyPlanRequest request, StudyPlanService svc) =>
            {
                try
                {
                    var plan = await svc.UpdateAsync(id, request);
                    return plan is not null
                        ? Results.Ok(plan)
                        : Results.NotFound($"No existe un plan de estudios con id {id}.");
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
            });

            app.MapDelete("/study-plans/{id:int}", async (int id, StudyPlanService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound($"No existe un plan de estudios con id {id}."));
        }
    }
}
