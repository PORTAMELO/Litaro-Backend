using Litaro.Models;
using Litaro.Services;

namespace Litaro.Endpoints
{
    public static class EnrollmentEndpoints
    {
        public static void MapEnrollmentEndpoints(this WebApplication app)
        {
            app.MapGet("/enrollments", async (EnrollmentService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/enrollments/{id}", async (int id, EnrollmentService svc) =>
                await svc.GetByIdAsync(id) is Enrollment e
                    ? Results.Ok(e)
                    : Results.NotFound());

            app.MapPost("/enrollments", async (Enrollment enrollment, EnrollmentService svc) =>
            {
                var created = await svc.CreateAsync(enrollment);
                return Results.Created($"/enrollments/{created.EnrollmentId}", created);
            });
        }
    }
}
