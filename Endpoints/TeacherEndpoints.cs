using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class TeacherEndpoints
    {
        public static void MapTeacherEndpoints(this WebApplication app)
        {
            app.MapGet("/teachers", async (HttpRequest request, TeacherService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Teacher", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/teachers/{id:int}", async (int id, TeacherService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var teacher = await svc.GetByIdAsync(id);

                return teacher is not null
                    ? Results.Ok(teacher)
                    : Results.NotFound($"No existe un docente con id {id}.");
            });

            app.MapPost("/teachers", async (TeacherService.CreateTeacherRequest request, TeacherService svc) =>
            {
                try
                {
                    var result = await svc.CreateAsync(request);
                    return Results.Ok(new
                    {
                        teacher = result.Teacher,
                        temporaryPassword = result.TemporaryPassword
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

            app.MapPost("/teachers/import", async (IFormFile file, TeacherService svc) =>
            {
                if (file is null || file.Length == 0)
                    return Results.BadRequest("No se recibió ningún archivo.");

                if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("El archivo debe ser un CSV.");

                await using var stream = file.OpenReadStream();
                var (imported, errors) = await svc.ImportFromCsvAsync(stream);

                return Results.Ok(new { Imported = imported, Errors = errors });
            }).DisableAntiforgery();

        }
    }
}
