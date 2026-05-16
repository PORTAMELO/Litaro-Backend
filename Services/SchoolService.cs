using Microsoft.EntityFrameworkCore;
using Litaro.Data;
using Litaro.Models;

namespace Litaro.Services;

public class SchoolService(AppDbContext db)
{
    public Task<List<School>> GetAllAsync()
    {
        return db.Schools.Where(s => s.Active).ToListAsync();
    }

    public async Task<School?> GetByIdAsync(int id)
    {
        return await db.Schools.FindAsync(id);
    }

    public async Task<School> CreateAsync(School school)
    {
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        return school;
    }

    public async Task<bool> UpdateAsync(int id, School updated)
    {
        var school = await db.Schools.FindAsync(id);
        if (school is null) return false;

        school.Name    = updated.Name;
        school.Address = updated.Address;
        school.Phone   = updated.Phone;

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var school = await db.Schools.FindAsync(id);
        if (school is null) return false;

        school.Active = false;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateAsync(int id)
    {
        var school = await db.Schools.FindAsync(id);
        if (school is null) return false;

        school.Active = true;
        await db.SaveChangesAsync();
        return true;
    }

}