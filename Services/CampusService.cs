using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class CampusService(AppDbContext db)
{
    public Task<List<Campus>> GetAllAsync()
    {
        return db.Campuses.Where(c => c.Active).ToListAsync();
    }

    public async Task<Campus?> GetByIdAsync(int id)
    {
        return await db.Campuses.FindAsync(id);
    }

    public async Task<Campus> CreateAsync(Campus campus)
    {
        db.Campuses.Add(campus);
        await db.SaveChangesAsync();
        return campus;
    }

    public async Task<bool> UpdateAsync(int id, Campus updated)
    {
        var campus = await db.Campuses.FindAsync(id);
        if (campus is null) return false;

        campus.Name = updated.Name;
        campus.Address = updated.Address;
        campus.Phone = updated.Phone;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var campus = await db.Campuses.FindAsync(id);
        if (campus is null) return false;

        campus.Active = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateAsync(int id)
    {
        var campus = await db.Campuses.FindAsync(id);
        if (campus is null) return false;

        campus.Active = true;
        await db.SaveChangesAsync();
        return true;
    }

}