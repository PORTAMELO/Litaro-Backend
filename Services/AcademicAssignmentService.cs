using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services
{
    public class AcademicAssignmentService(AppDbContext db)
    {
        public Task<List<AcademicAssignment>> GetAllAsync() =>
            db.AcademicAssignments.Where(a => a.Active).ToListAsync();

        public async Task<AcademicAssignment?> GetByIdAsync(int id) =>
            await db.AcademicAssignments.FirstOrDefaultAsync(a => a.AssignmentId == id && a.Active);

        public async Task<AcademicAssignment> CreateAsync(AcademicAssignment assignment)
        {
            db.AcademicAssignments.Add(assignment);
            await db.SaveChangesAsync();
            return assignment;
        }

        public async Task<bool> UpdateAsync(int id, AcademicAssignment updated)
        {
            var assignment = await db.AcademicAssignments.FindAsync(id);
            if (assignment is null) return false;

            assignment.ClassroomId = updated.ClassroomId;
            assignment.SubjectId = updated.SubjectId;
            assignment.TeacherId = updated.TeacherId;
            assignment.YearId = updated.YearId;

            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeactivateAsync(int id)
        {
            var assignment = await db.AcademicAssignments.FindAsync(id);
            if (assignment is null) return false;

            assignment.Active = false;
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ActivateAsync(int id)
        {
            var assignment = await db.AcademicAssignments.FindAsync(id);
            if (assignment is null) return false;

            assignment.Active = true;
            await db.SaveChangesAsync();
            return true;
        }
    }
}
