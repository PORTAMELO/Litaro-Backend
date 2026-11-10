using System.Globalization;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record AttendanceSettings(
    decimal LossPercent,
    decimal LossGrade,
    int ExcuseDeadlineDays,
    int LateCommitmentCount,
    int LateCitationCount,
    int AbsenceAlertDays,
    int NightlyHour,
    List<string> DailyLevels);

public class AttendanceSettingsService(AppDbContext db)
{
    public const string CharacteristicName = "Parámetros de asistencia";
    public const string DailyLevelName = "Nivel con lista diaria";

    private const string LossPercentName = "Porcentaje de pérdida";
    private const string LossGradeName = "Nota por pérdida";
    private const string ExcuseDeadlineName = "Días para presentar excusa";
    private const string LateCommitmentName = "Tardanzas para compromiso";
    private const string LateCitationName = "Tardanzas para citación";
    private const string AbsenceAlertName = "Días de inasistencia para alerta";
    private const string NightlyHourName = "Hora del proceso nocturno";

    private static readonly AttendanceSettings Defaults = new(20m, 1.0m, 2, 3, 5, 5, 23, []);

    public async Task<AttendanceSettings> GetAsync()
    {
        var details = await db.CharacteristicDetails
            .AsNoTracking()
            .Where(d => d.Characteristic!.Name == CharacteristicName)
            .Select(d => new { d.Nombre, d.Valor })
            .ToListAsync();

        decimal Number(string name, decimal fallback)
        {
            var raw = details.FirstOrDefault(d => d.Nombre == name)?.Valor;
            return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        return new AttendanceSettings(
            Number(LossPercentName, Defaults.LossPercent),
            Number(LossGradeName, Defaults.LossGrade),
            (int)Number(ExcuseDeadlineName, Defaults.ExcuseDeadlineDays),
            (int)Number(LateCommitmentName, Defaults.LateCommitmentCount),
            (int)Number(LateCitationName, Defaults.LateCitationCount),
            (int)Number(AbsenceAlertName, Defaults.AbsenceAlertDays),
            (int)Number(NightlyHourName, Defaults.NightlyHour),
            details.Where(d => d.Nombre == DailyLevelName).Select(d => d.Valor).OrderBy(v => v).ToList());
    }

    public async Task<AttendanceSettings> SaveAsync(AttendanceSettings request)
    {
        if (request.LossPercent is < 1 or > 100)
            throw new InvalidOperationException("El porcentaje de pérdida debe estar entre 1 y 100.");

        if (request.LossGrade is < 0 or > 5)
            throw new InvalidOperationException("La nota por pérdida debe estar entre 0 y 5.");

        if (request.ExcuseDeadlineDays is < 0 or > 30)
            throw new InvalidOperationException("Los días para presentar excusa deben estar entre 0 y 30.");

        if (request.LateCommitmentCount < 1 || request.LateCitationCount <= request.LateCommitmentCount)
            throw new InvalidOperationException("Las tardanzas para citación deben ser más que las de compromiso.");

        if (request.AbsenceAlertDays is < 1 or > 30)
            throw new InvalidOperationException("Los días de inasistencia para la alerta deben estar entre 1 y 30.");

        if (request.NightlyHour is < 0 or > 23)
            throw new InvalidOperationException("La hora del proceso nocturno debe estar entre 0 y 23.");

        var characteristic = await db.Characteristics.FirstOrDefaultAsync(c => c.Name == CharacteristicName);
        if (characteristic is null)
        {
            characteristic = new Characteristic { Name = CharacteristicName };
            db.Characteristics.Add(characteristic);
            await db.SaveChangesAsync();
        }

        var details = await db.CharacteristicDetails
            .Where(d => d.CharacteristicId == characteristic.CharacteristicId)
            .ToListAsync();

        void Set(string name, decimal value)
        {
            var text = value.ToString(CultureInfo.InvariantCulture);
            var detail = details.FirstOrDefault(d => d.Nombre == name);

            if (detail is null)
            {
                db.CharacteristicDetails.Add(new CharacteristicDetail
                {
                    CharacteristicId = characteristic.CharacteristicId,
                    Nombre = name,
                    Valor = text,
                });
                return;
            }

            detail.Valor = text;
        }

        Set(LossPercentName, request.LossPercent);
        Set(LossGradeName, request.LossGrade);
        Set(ExcuseDeadlineName, request.ExcuseDeadlineDays);
        Set(LateCommitmentName, request.LateCommitmentCount);
        Set(LateCitationName, request.LateCitationCount);
        Set(AbsenceAlertName, request.AbsenceAlertDays);
        Set(NightlyHourName, request.NightlyHour);

        var levels = (request.DailyLevels ?? [])
            .Select(level => level.Trim())
            .Where(level => level.Length > 0)
            .Distinct()
            .ToList();

        db.CharacteristicDetails.RemoveRange(details.Where(d => d.Nombre == DailyLevelName));
        db.CharacteristicDetails.AddRange(levels.Select(level => new CharacteristicDetail
        {
            CharacteristicId = characteristic.CharacteristicId,
            Nombre = DailyLevelName,
            Valor = level,
        }));

        await db.SaveChangesAsync();
        return await GetAsync();
    }

    public Task<List<string>> GetGradeLevelsAsync() =>
        db.Grades
            .AsNoTracking()
            .Select(g => g.Level)
            .Distinct()
            .OrderBy(level => level)
            .ToListAsync();
}
