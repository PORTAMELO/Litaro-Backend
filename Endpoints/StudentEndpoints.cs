using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class StudentEndpoints
    {
        public static void MapStudentEndpoints(this WebApplication app)
        {
            app.MapGet("/students", async (HttpRequest request, StudentService svc, ForeignKeyResolverService fkSvc) =>
            {
                var filters = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
                var records = await svc.GetAllAsync(filters);
                var lookups = await fkSvc.BuildLookupsAsync("Student", records);
                return Results.Ok(new { records, lookups });
            });

            app.MapGet("/students/{id:int}", async (int id, StudentService svc) =>
            {
                if (id <= 0)
                    return Results.BadRequest("El id debe ser un entero positivo.");

                var student = await svc.GetByIdAsync(id);

                return student is not null
                    ? Results.Ok(student)
                    : Results.NotFound($"No existe un estudiante con id {id}.");
            });

            app.MapPost("/students", async (StudentService.CreateStudentRequest request, StudentService svc) =>
            {
                try
                {
                    var result = await svc.CreateAsync(request);
                    return Results.Ok(new
                    {
                        student = result.Student,
                        temporaryPassword = result.TemporaryPassword
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            }).RequireAuthorization(policy => policy.RequireRole("Administrador"));

            app.MapPost("/students/import", async (IFormFile file, StudentService svc) =>
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
