using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class GradeEndpoints
    {
        public static void MapGradeEndpoints(this WebApplication app)
        {
            app.MapGet("/grades", async (HttpRequest request, GradeService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Grade", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/grades/{id:int}", async (int id, GradeService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var grade = await svc.GetByIdAsync(id);

                return grade is not null
                    ? Results.Ok(grade)
                    : Results.NotFound($"No existe un grado con id {id}.");
            });
        }
    }
}
