using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class ScheduleService(AppDbContext db)
    {
        public Task<List<Schedule>> GetAllAsync() =>
            db.Schedules.ToListAsync();

        public async Task<Schedule?> GetByIdAsync(int id) =>
            await db.Schedules.FindAsync(id);

        public async Task<Schedule> CreateAsync(Schedule schedule)
        {
            db.Schedules.Add(schedule);
            await db.SaveChangesAsync();
            return schedule;
        }

        public async Task<bool> UpdateAsync(int id, Schedule updated)
        {
            var schedule = await db.Schedules.FindAsync(id);
            if (schedule is null) return false;

            schedule.AssignmentId = updated.AssignmentId;
            schedule.Weekday = updated.Weekday;
            schedule.StartTime = updated.StartTime;
            schedule.EndTime = updated.EndTime;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var schedule = await db.Schedules.FindAsync(id);
            if (schedule is null) return false;

            db.Schedules.Remove(schedule);
            await db.SaveChangesAsync();
            return true;
        }
    }
}
