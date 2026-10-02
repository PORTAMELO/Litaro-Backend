using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class StudyPlanService(AppDbContext db)
{
    public record SaveStudyPlanRequest(short YearId, int GradeId, int SubjectId, byte WeeklyHours);

    public Task<List<StudyPlan>> GetAllAsync(IDictionary<string, string>? filters = null)
    {
        var query = db.StudyPlans.AsQueryable();

        if (filters is not null && filters.Count > 0)
            query = query.ApplyFilters(filters);

        return query.OrderBy(p => p.GradeId).ThenBy(p => p.SubjectId).ToListAsync();
    }

    public Task<StudyPlan?> GetByIdAsync(int id) =>
        db.StudyPlans.FirstOrDefaultAsync(p => p.StudyPlanId == id);

    public async Task<StudyPlan> CreateAsync(SaveStudyPlanRequest request)
    {
        var plan = new StudyPlan();
        await ApplyAsync(plan, request);
        db.StudyPlans.Add(plan);
        await SaveAsync();
        return plan;
    }

    public async Task<StudyPlan?> UpdateAsync(int id, SaveStudyPlanRequest request)
    {
        var plan = await db.StudyPlans.FirstOrDefaultAsync(p => p.StudyPlanId == id);
        if (plan is null) return null;

        await ApplyAsync(plan, request);
        await SaveAsync();
        return plan;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var plan = await db.StudyPlans.FirstOrDefaultAsync(p => p.StudyPlanId == id);
        if (plan is null) return false;

        db.StudyPlans.Remove(plan);
        await db.SaveChangesAsync();
        return true;
    }

    private async Task ApplyAsync(StudyPlan plan, SaveStudyPlanRequest request)
    {
        if (request.WeeklyHours is < 1 or > 40)
            throw new InvalidOperationException("Las horas semanales deben estar entre 1 y 40.");
        if (!await db.AcademicYears.AnyAsync(y => y.YearId == request.YearId))
            throw new InvalidOperationException($"El año lectivo {request.YearId} no existe.");
        if (!await db.Grades.AnyAsync(g => g.GradeId == request.GradeId))
            throw new InvalidOperationException($"El grado con id {request.GradeId} no existe.");
        if (!await db.Subjects.AnyAsync(s => s.SubjectId == request.SubjectId))
            throw new InvalidOperationException($"La materia con id {request.SubjectId} no existe.");

        plan.YearId = request.YearId;
        plan.GradeId = request.GradeId;
        plan.SubjectId = request.SubjectId;
        plan.WeeklyHours = request.WeeklyHours;
    }

    private async Task SaveAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new InvalidOperationException("Ya existe una intensidad para esa materia en ese grado y año.");
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(ex.InnerException?.Message ?? ex.Message);
        }
    }
}
