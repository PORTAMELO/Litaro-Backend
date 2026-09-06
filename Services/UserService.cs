using System.Security.Cryptography;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class UserService(AppDbContext db)
    {

        public static string GenerateTemporaryPassword()
        {

            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";

            var chars = upper + lower + digits;
            var bytes = RandomNumberGenerator.GetBytes(10);

            var result = new char[10];
            for (int i = 0; i < 10; i++)
                result[i] = chars[bytes[i] % chars.Length];

            result[0] = upper[bytes[0] % upper.Length];
            result[1] = digits[bytes[1] % digits.Length];

            return new string(result);
        }

        public Task<List<User>> GetAllAsync(IDictionary<string, string>? filters = null)
        {
            var query = db.Users.Where(u => u.Active).AsQueryable();

            if (filters is not null && filters.Count > 0)
                query = query.ApplyFilters(filters);

            return query.ToListAsync();
        }

        public Task<User?> GetByIdAsync(int id) =>
            db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Active);
    }
}
