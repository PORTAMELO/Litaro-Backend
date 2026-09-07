using Litaro.Data;
using Litaro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class UserService(AppDbContext db, UserManager<User> userManager)
    {
        public Task<List<User>> GetAllAsync() =>
            db.Users.Where(u => u.Active).ToListAsync();

        public async Task<User?> GetByIdAsync(int id) =>
            await db.Users.FindAsync(id);

        public async Task<bool> UpdateAsync(int id, User updated)
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return false;

            user.FirstName = updated.FirstName;
            user.LastName = updated.LastName;
            user.Email = updated.Email;
            user.UserName = updated.Email;
            user.CampusId = updated.CampusId;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ChangeRoleAsync(int id, string newRole)
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user is null) return false;

            var currentRoles = await userManager.GetRolesAsync(user);
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, newRole);
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