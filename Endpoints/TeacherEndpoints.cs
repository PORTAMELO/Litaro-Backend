using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class TeacherEndpoints
    {
        public static void MapTeacherEndpoints(this WebApplication app)
        {
            app.MapGet("/teachers", async (TeacherService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/teachers/{id}", async (int id, TeacherService svc) =>
                await svc.GetByIdAsync(id) is Teacher t
                    ? Results.Ok(t)
                    : Results.NotFound());

            app.MapPost("/teachers", async (Teacher teacher, TeacherService svc) =>
            {
                var created = await svc.CreateAsync(teacher);
                return Results.Created($"/teachers/{created.TeacherId}", created);
            });

            app.MapPut("/teachers/{id}", async (int id, Teacher teacher, TeacherService svc) =>
                await svc.UpdateAsync(id, teacher)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/teachers/{id}", async (int id, TeacherService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());

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
