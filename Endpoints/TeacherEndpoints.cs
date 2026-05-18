using Litaro.Models;
using Litaro.Services;

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
        }
    }
}
