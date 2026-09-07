using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class GradeScoreEndpoints
    {
        public static void MapGradeScoreEndpoints(this WebApplication app)
        {
            app.MapGet("/grade-scores", async (GradeScoreService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/grade-scores/{id}", async (int id, GradeScoreService svc) =>
                await svc.GetByIdAsync(id) is GradeScore gs
                    ? Results.Ok(gs)
                    : Results.NotFound());

            app.MapPost("/grade-scores", async (GradeScore gradeScore, GradeScoreService svc) =>
            {
                var created = await svc.CreateAsync(gradeScore);
                return Results.Created($"/grade-scores/{created.GradeScoreId}", created);
            });

            app.MapPut("/grade-scores/{id}", async (int id, GradeScore gradeScore, GradeScoreService svc) =>
                await svc.UpdateAsync(id, gradeScore)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/grade-scores/{id}", async (int id, GradeScoreService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}
