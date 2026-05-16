using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class UserService(AppDbContext db)
    {
        public Task<List<User>> GetAllAsync() =>
            db.Users.Where(u => u.Active).ToListAsync();

        public async Task<User?> GetByIdAsync(int id) =>
            await db.Users.FindAsync(id);

        public async Task<User> CreateAsync(User user)
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public async Task<bool> UpdateAsync(int id, User updated)
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return false;

            user.FirstName = updated.FirstName;
            user.LastName = updated.LastName;
            user.Email = updated.Email;
            user.Role = updated.Role;
            user.CampusId = updated.CampusId;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return false;

            user.Active = false;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateAsync(int id)
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return false;

            user.Active = true;
            await db.SaveChangesAsync();
            return true;
        }
    }
}
