using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class TeacherAvailabilityService(AppDbContext db)
{
    public record SaveAvailabilityRequest(short YearId, int TeacherId, byte Weekday, TimeOnly StartTime, TimeOnly EndTime, int? CampusId);

    public Task<List<TeacherAvailability>> GetAllAsync(IDictionary<string, string>? filters = null)
    {
        var query = db.TeacherAvailabilities.AsQueryable();

        if (filters is not null && filters.Count > 0)
            query = query.ApplyFilters(filters);

        return query.OrderBy(a => a.TeacherId).ThenBy(a => a.Weekday).ThenBy(a => a.StartTime).ToListAsync();
    }

    public Task<TeacherAvailability?> GetByIdAsync(int id) =>
        db.TeacherAvailabilities.FirstOrDefaultAsync(a => a.TeacherAvailabilityId == id);

    public async Task<TeacherAvailability> CreateAsync(SaveAvailabilityRequest request)
    {
        var availability = new TeacherAvailability();
        await ApplyAsync(availability, request);
        db.TeacherAvailabilities.Add(availability);
        await SaveAsync();
        return availability;
    }

    public async Task<TeacherAvailability?> UpdateAsync(int id, SaveAvailabilityRequest request)
    {
        var availability = await db.TeacherAvailabilities.FirstOrDefaultAsync(a => a.TeacherAvailabilityId == id);
        if (availability is null) return null;

        await ApplyAsync(availability, request);
        await SaveAsync();
        return availability;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var availability = await db.TeacherAvailabilities.FirstOrDefaultAsync(a => a.TeacherAvailabilityId == id);
        if (availability is null) return false;

        db.TeacherAvailabilities.Remove(availability);
        await db.SaveChangesAsync();
        return true;
    }

    private async Task ApplyAsync(TeacherAvailability availability, SaveAvailabilityRequest request)
    {
        if (request.Weekday is < 1 or > 6)
            throw new InvalidOperationException("El día debe estar entre 1 (lunes) y 6 (sábado).");
        if (request.EndTime <= request.StartTime)
            throw new InvalidOperationException("La hora de fin debe ser mayor que la hora de inicio.");
        if (!await db.AcademicYears.AnyAsync(y => y.YearId == request.YearId))
            throw new InvalidOperationException($"El año lectivo {request.YearId} no existe.");
        if (!await db.Teachers.AnyAsync(t => t.TeacherId == request.TeacherId && t.Active))
            throw new InvalidOperationException("El docente no existe o está inactivo.");
        if (request.CampusId is not null && !await db.Campuses.AnyAsync(c => c.CampusId == request.CampusId && c.Active))
            throw new InvalidOperationException("La sede no existe o está inactiva.");

        var overlap = await db.TeacherAvailabilities.AnyAsync(a =>
            a.TeacherId == request.TeacherId && a.YearId == request.YearId && a.Weekday == request.Weekday &&
            a.StartTime < request.EndTime && a.EndTime > request.StartTime &&
            a.TeacherAvailabilityId != availability.TeacherAvailabilityId);
        if (overlap)
            throw new InvalidOperationException("Ese rango se cruza con otra disponibilidad del docente ese día.");

        availability.YearId = request.YearId;
        availability.TeacherId = request.TeacherId;
        availability.Weekday = request.Weekday;
        availability.StartTime = request.StartTime;
        availability.EndTime = request.EndTime;
        availability.CampusId = request.CampusId;
    }

    private async Task SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
