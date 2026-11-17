using System.Globalization;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public class PromotionSettingsService(AppDbContext db)
{
    public const string CharacteristicName = "Parámetros de promoción";
    public const string FundamentalSubjectName = "Asignatura fundamental";

    private const string Period1Name = "Peso del periodo 1";
    private const string Period2Name = "Peso del periodo 2";
    private const string Period3Name = "Peso del periodo 3";
    private const string ExceptionMinGradeName = "Nota mínima de la asignatura perdida";
    private const string ExceptionRestAverageName = "Promedio mínimo de las demás áreas";
    private const string ExceptionAttendanceName = "Asistencia mínima del año";
    private const string PiarLostAreasName = "Áreas perdidas para reprobar con PIAR";

    public static readonly PromotionSettings Defaults = new(30m, 30m, 40m, 3.20m, 4.00m, 90m, 3, []);

    public async Task<PromotionSettings> GetAsync()
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

        return new PromotionSettings(
            Number(Period1Name, Defaults.Period1Weight),
            Number(Period2Name, Defaults.Period2Weight),
            Number(Period3Name, Defaults.Period3Weight),
            Number(ExceptionMinGradeName, Defaults.ExceptionMinGrade),
            Number(ExceptionRestAverageName, Defaults.ExceptionRestAverage),
            Number(ExceptionAttendanceName, Defaults.ExceptionAttendancePercent),
            (int)Number(PiarLostAreasName, Defaults.PiarLostAreas),
            details
                .Where(d => d.Nombre == FundamentalSubjectName)
                .Select(d => int.TryParse(d.Valor, out var id) ? id : 0)
                .Where(id => id > 0)
                .Order()
                .ToList());
    }

    public async Task<PromotionSettings> SaveAsync(PromotionSettings request)
    {
        var weights = new[] { request.Period1Weight, request.Period2Weight, request.Period3Weight };
        if (weights.Any(w => w <= 0m) || weights.Sum() != 100m)
            throw new InvalidOperationException("Los pesos de los tres periodos deben ser mayores a cero y sumar 100.");

        if (request.ExceptionMinGrade is < 1 or > GradingRules.PassingGrade)
            throw new InvalidOperationException("La nota mínima de la asignatura perdida debe estar entre 1 y 3.5.");

        if (request.ExceptionRestAverage is < GradingRules.PassingGrade or > 5)
            throw new InvalidOperationException("El promedio mínimo de las demás áreas debe estar entre 3.5 y 5.");

        if (request.ExceptionAttendancePercent is < 0 or > 100)
            throw new InvalidOperationException("La asistencia mínima debe estar entre 0 y 100.");

        if (request.PiarLostAreas is < 1 or > 20)
            throw new InvalidOperationException("Las áreas perdidas para reprobar con PIAR deben estar entre 1 y 20.");

        var subjectIds = (request.FundamentalSubjectIds ?? []).Distinct().ToList();
        var known = await db.Subjects.Where(s => subjectIds.Contains(s.SubjectId)).Select(s => s.SubjectId).ToListAsync();
        if (known.Count != subjectIds.Count)
            throw new InvalidOperationException("Una de las asignaturas fundamentales no existe.");

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

        Set(Period1Name, request.Period1Weight);
        Set(Period2Name, request.Period2Weight);
        Set(Period3Name, request.Period3Weight);
        Set(ExceptionMinGradeName, request.ExceptionMinGrade);
        Set(ExceptionRestAverageName, request.ExceptionRestAverage);
        Set(ExceptionAttendanceName, request.ExceptionAttendancePercent);
        Set(PiarLostAreasName, request.PiarLostAreas);

        db.CharacteristicDetails.RemoveRange(details.Where(d => d.Nombre == FundamentalSubjectName));
        db.CharacteristicDetails.AddRange(subjectIds.Select(id => new CharacteristicDetail
        {
            CharacteristicId = characteristic.CharacteristicId,
            Nombre = FundamentalSubjectName,
            Valor = id.ToString(CultureInfo.InvariantCulture),
        }));

        await db.SaveChangesAsync();
        return await GetAsync();
    }
}
