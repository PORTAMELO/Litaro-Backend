using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class SpaceService(AppDbContext db)
{
    public record SaveSpaceRequest(int CampusId, string Name, string Type, short? Capacity);

    public Task<List<Space>> GetAllAsync(IDictionary<string, string>? filters = null)
    {
        var query = db.Spaces.Where(s => s.Active).AsQueryable();

        if (filters is not null && filters.Count > 0)
            query = query.ApplyFilters(filters);

        return query.OrderBy(s => s.Name).ToListAsync();
    }

    public Task<Space?> GetByIdAsync(int id) =>
        db.Spaces.FirstOrDefaultAsync(s => s.SpaceId == id);

    public async Task<Space> CreateAsync(SaveSpaceRequest request)
    {
        var space = new Space();
        await ApplyAsync(space, request);
        db.Spaces.Add(space);
        await SaveAsync();
        return space;
    }

    public async Task<Space?> UpdateAsync(int id, SaveSpaceRequest request)
    {
        var space = await db.Spaces.FirstOrDefaultAsync(s => s.SpaceId == id);
        if (space is null) return null;

        if (space.CampusId != request.CampusId && await db.Schedules.AnyAsync(s => s.SpaceId == id))
            throw new InvalidOperationException($"\"{space.Name}\" ya se usa en el horario; no se puede cambiar de sede. Da de baja este espacio y crea uno nuevo en la otra sede.");

        await ApplyAsync(space, request);
        await SaveAsync();
        return space;
    }

    public async Task<bool> SetActiveAsync(int id, bool active)
    {
        var space = await db.Spaces.FirstOrDefaultAsync(s => s.SpaceId == id);
        if (space is null) return false;

        space.Active = active;
        await db.SaveChangesAsync();
        return true;
    }

    private async Task ApplyAsync(Space space, SaveSpaceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Type))
            throw new InvalidOperationException("Nombre y tipo del espacio son obligatorios.");
        if (request.Capacity is <= 0)
            throw new InvalidOperationException("La capacidad debe ser mayor que cero.");
        if (!await db.Campuses.AnyAsync(c => c.CampusId == request.CampusId && c.Active))
            throw new InvalidOperationException($"La sede con id {request.CampusId} no existe o está inactiva.");

        space.CampusId = request.CampusId;
        space.Name = request.Name.Trim();
        space.Type = request.Type.Trim();
        space.Capacity = request.Capacity;
    }

    private async Task SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new InvalidOperationException("Ya existe un espacio con ese nombre en la sede.");
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
