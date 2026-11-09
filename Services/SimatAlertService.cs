using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record SimatAlertDto(
    int SimatAlertId,
    string Type,
    short? YearId,
    int? StudentId,
    int? AdmissionRequestId,
    string Message,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    string? ResolvedBy,
    string? ResolutionNote);

public class SimatAlertService(AppDbContext db)
{
    public SimatAlert Add(
        string type,
        string message,
        short? yearId = null,
        int? studentId = null,
        int? admissionRequestId = null)
    {
        var alert = new SimatAlert
        {
            Type = type,
            Message = message.Length <= 400 ? message : message[..400],
            YearId = yearId,
            StudentId = studentId,
            AdmissionRequestId = admissionRequestId
        };

        db.SimatAlerts.Add(alert);
        return alert;
    }

    public async Task<List<SimatAlertDto>> ListAsync(string? status)
    {
        var query = db.SimatAlerts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToUpperInvariant();
            query = query.Where(a => a.Status == wanted);
        }

        return await query
            .OrderBy(a => a.Status == SimatAlertStatuses.Pending ? 0 : 1)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new SimatAlertDto(
                a.SimatAlertId,
                a.Type,
                a.YearId,
                a.StudentId,
                a.AdmissionRequestId,
                a.Message,
                a.Status,
                a.CreatedAt,
                a.ResolvedAt,
                a.ResolvedBy != null ? a.ResolvedBy.FirstName + " " + a.ResolvedBy.LastName : null,
                a.ResolutionNote))
            .Take(500)
            .ToListAsync();
    }

    public async Task<int> ResolveAsync(List<int> ids, string? note, int userId)
    {
        if (ids.Count == 0)
            throw new InvalidOperationException("Seleccione al menos una alerta.");

        var cleanNote = note?.Trim();
        if (cleanNote is { Length: > 300 })
            cleanNote = cleanNote[..300];

        var alerts = await db.SimatAlerts
            .Where(a => ids.Contains(a.SimatAlertId) && a.Status == SimatAlertStatuses.Pending)
            .ToListAsync();

        foreach (var alert in alerts)
        {
            alert.Status = SimatAlertStatuses.Done;
            alert.ResolvedAt = DateTime.UtcNow;
            alert.ResolvedById = userId;
            alert.ResolutionNote = string.IsNullOrEmpty(cleanNote) ? null : cleanNote;
        }

        await db.SaveChangesAsync();
        return alerts.Count;
    }
}