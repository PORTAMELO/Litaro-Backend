using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class AcademicPeriodEndpoints
    {
        public static void MapAcademicPeriodEndpoints(this WebApplication app)
        {
            app.MapGet("/academic-periods", async (AcademicPeriodService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/academic-periods/{id}", async (short id, AcademicPeriodService svc) =>
                await svc.GetByIdAsync(id) is AcademicPeriod p
                    ? Results.Ok(p)
                    : Results.NotFound());

            app.MapPost("/academic-periods", async (AcademicPeriod period, AcademicPeriodService svc) =>
            {
                var created = await svc.CreateAsync(period);
                return Results.Created($"/academic-periods/{created.PeriodId}", created);
            });

            app.MapPut("/academic-periods/{id}", async (short id, AcademicPeriod period, AcademicPeriodService svc) =>
                await svc.UpdateAsync(id, period)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/academic-periods/{id}", async (short id, AcademicPeriodService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}
