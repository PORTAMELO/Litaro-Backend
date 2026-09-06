using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class SubjectEndpoints
    {
        public static void MapSubjectEndpoints(this WebApplication app)
        {
            app.MapGet("/subjects", async (HttpRequest request, SubjectService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Subject", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/subjects/{id:int}", async (int id, SubjectService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var subject = await svc.GetByIdAsync(id);

                return subject is not null
                    ? Results.Ok(subject)
                    : Results.NotFound($"No existe una materia con id {id}.");
            });
        }
    }
}
