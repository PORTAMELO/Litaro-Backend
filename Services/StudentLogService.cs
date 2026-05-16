using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class StudentLogService(AppDbContext db)
    {
        public Task<List<StudentLog>> GetAllAsync() =>
            db.StudentLogs.ToListAsync();

        public async Task<StudentLog?> GetByIdAsync(int id) =>
            await db.StudentLogs.FindAsync(id);

        public async Task<StudentLog> CreateAsync(StudentLog log)
        {
            db.StudentLogs.Add(log);
            await db.SaveChangesAsync();
            return log;
        }

        public async Task<bool> UpdateAsync(int id, StudentLog updated)
        {
            var log = await db.StudentLogs.FindAsync(id);
            if (log is null) return false;

            log.Type = updated.Type;
            log.Observation = updated.Observation;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var log = await db.StudentLogs.FindAsync(id);
            if (log is null) return false;

            db.StudentLogs.Remove(log);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
