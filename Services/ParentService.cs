using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ParentService(AppDbContext db)
    {
        public Task<List<Parent>> GetAllAsync() =>
            db.Parents.ToListAsync();

        public async Task<Parent?> GetByIdAsync(int id) =>
            await db.Parents.FindAsync(id);

        public async Task<Parent> CreateAsync(Parent parent)
        {
            db.Parents.Add(parent);
            await db.SaveChangesAsync();
            return parent;
        }

        public async Task<bool> UpdateAsync(int id, Parent updated)
        {
            var parent = await db.Parents.FindAsync(id);
            if (parent is null) return false;

            parent.Relationship = updated.Relationship;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var parent = await db.Parents.FindAsync(id);
            if (parent is null) return false;

            db.Parents.Remove(parent);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
