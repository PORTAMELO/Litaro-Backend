using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class GradeScoreEndpoints
    {
        public static void MapGradeScoreEndpoints(this WebApplication app)
        {
            app.MapGet("/grade-scores", async (HttpRequest request, GradeScoreService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("GradeScore", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/grade-scores/{id:int}", async (int id, GradeScoreService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var gradeScore = await svc.GetByIdAsync(id);

                return gradeScore is not null
                    ? Results.Ok(gradeScore)
                    : Results.NotFound($"No existe una nota con id {id}.");
            });
        }
    }
}
