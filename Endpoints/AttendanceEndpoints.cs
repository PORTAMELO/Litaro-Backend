using Litaro.Models;
using Litaro.Services;
using Microsoft.AspNetCore.Mvc;

namespace Litaro.Endpoints
{
    public static class AttendanceEndpoints
    {
        public static void MapAttendanceEndpoints(this WebApplication app)
        {
            app.MapGet("/attendances", async (AttendanceService svc) =>
                Results.Ok(await svc.GetAllAsync()));

            app.MapGet("/attendances/{id}", async (int id, AttendanceService svc) =>
                await svc.GetByIdAsync(id) is Attendance a
                    ? Results.Ok(a)
                    : Results.NotFound());

            app.MapPost("/attendances", async (Attendance attendance, AttendanceService svc) =>
            {
                var created = await svc.CreateAsync(attendance);
                return Results.Created($"/attendances/{created.AttendanceId}", created);
            });

            app.MapPut("/attendances/{id}", async (int id, Attendance attendance, AttendanceService svc) =>
                await svc.UpdateAsync(id, attendance)
                    ? Results.NoContent()
                    : Results.NotFound());

            app.MapDelete("/attendances/{id}", async (int id, AttendanceService svc) =>
                await svc.DeleteAsync(id)
                    ? Results.NoContent()
                    : Results.NotFound());
        }
    }
}
