using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class ParentStudentEndpoints
    {
        public static void MapParentStudentEndpoints(this WebApplication app)
        {
            app.MapGet("/parent-students", async (ParentStudentService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/parent-students/{id}", async (int id, ParentStudentService svc) =>
                await svc.GetByIdAsync(id) is ParentStudent ps
                    ? Results.Ok(ps)
                    : Results.NotFound());

            app.MapPost("/parent-students", async (ParentStudent parentStudent, ParentStudentService svc) =>
            {
                var created = await svc.CreateAsync(parentStudent);
                return Results.Created($"/parent-students/{created.ParentStudentId}", created);
            });

            app.MapPut("/parent-students/{id}", async (int id, ParentStudent parentStudent, ParentStudentService svc) =>
                await svc.UpdateAsync(id, parentStudent)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/parent-students/{id}", async (int id, ParentStudentService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}
