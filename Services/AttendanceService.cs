using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class AttendanceService(AppDbContext db)
    {
        public Task<List<Attendance>> GetAllAsync() =>
            db.Attendances.ToListAsync();

        public async Task<Attendance?> GetByIdAsync(int id) =>
            await db.Attendances.FindAsync(id);

        public async Task<Attendance> CreateAsync(Attendance attendance)
        {
            db.Attendances.Add(attendance);
            await db.SaveChangesAsync();
            return attendance;
        }

        public async Task<bool> UpdateAsync(int id, Attendance updated)
        {
            var attendance = await db.Attendances.FindAsync(id);
            if (attendance is null) return false;

            attendance.Status = updated.Status;
            attendance.Observation = updated.Observation;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var attendance = await db.Attendances.FindAsync(id);
            if (attendance is null) return false;

            db.Attendances.Remove(attendance);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
