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

            app.MapPatch("/enrollments/{id}/status", async (int id, StatusRequest req, EnrollmentService svc) =>
                await svc.UpdateStatusAsync(id, req.Status)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/enrollments/{id}", async (int id, EnrollmentService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }

        public record StatusRequest(string Status);

    }
}
