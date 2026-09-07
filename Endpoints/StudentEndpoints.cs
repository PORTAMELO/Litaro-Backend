using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class StudentEndpoints
    {
        public static void MapStudentEndpoints(this WebApplication app)
        {
            app.MapGet("/students", async (StudentService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/students/{id}", async (int id, StudentService svc) =>
                await svc.GetByIdAsync(id) is Student s
                    ? Results.Ok(s)
                    : Results.NotFound());

            app.MapPost("/students", async (Student student, StudentService svc) =>
            {
                var created = await svc.CreateAsync(student);
                return Results.Created($"/students/{created.StudentId}", created);
            });

            app.MapPut("/students/{id}", async (int id, Student student, StudentService svc) =>
                await svc.UpdateAsync(id, student)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/students/{id}", async (int id, StudentService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

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
