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

        public record ProfileStatus(bool Exists, bool Active, int? Id);

        public record ProfileSummary(ProfileStatus Student, ProfileStatus Parent, ProfileStatus Teacher);

        public record UserLookupResult(
            bool Found,
            int? Id,
            string? FirstName,
            string? LastName,
            string? Email,
            string? PhoneNumber,
            int? CampusId,
            bool AccountActive,
            ProfileStatus Student,
            ProfileStatus Parent,
            ProfileStatus Teacher
        );

        private static readonly ProfileStatus NoProfile = new(false, false, null);

        private async Task<(ProfileStatus Student, ProfileStatus Parent, ProfileStatus Teacher)> GetProfilesByUserIdAsync(int userId)
        {
            var student = await db.Students.FirstOrDefaultAsync(s => s.StudentId == userId);
            var parent = await db.Parents.FirstOrDefaultAsync(p => p.ParentId == userId);
            var teacher = await db.Teachers.FirstOrDefaultAsync(t => t.TeacherId == userId);

            return (
                student is null ? NoProfile : new ProfileStatus(true, student.Active, student.StudentId),
                parent is null ? NoProfile : new ProfileStatus(true, parent.Active, parent.ParentId),
                teacher is null ? NoProfile : new ProfileStatus(true, teacher.Active, teacher.TeacherId)
            );
        }

        public async Task<UserLookupResult> LookupByDocumentAsync(string documentType, string documentNumber)
        {
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.DocumentType == documentType && u.DocumentNumber == documentNumber);

            if (user is null)
                return new UserLookupResult(false, null, null, null, null, null, null, true, NoProfile, NoProfile, NoProfile);

            var (student, parent, teacher) = await GetProfilesByUserIdAsync(user.Id);

            return new UserLookupResult(
                true,
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.CampusId,
                user.Active,
                student,
                parent,
                teacher
            );
        }

        public async Task<ProfileSummary> GetProfileSummaryAsync(int userId)
        {
            var (student, parent, teacher) = await GetProfilesByUserIdAsync(userId);
            return new ProfileSummary(student, parent, teacher);
        }
    }
}