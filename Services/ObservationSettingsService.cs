using System.Globalization;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record ObservationSettings(int TypeIThreshold);

public class ObservationSettingsService(AppDbContext db)
{
    public const string CharacteristicName = "Parámetros del observador";
    public const string TypeIThresholdName = "Situaciones Tipo I para alerta";

    public static readonly ObservationSettings Defaults = new(3);

    public async Task<ObservationSettings> GetAsync()
    {
        var raw = await db.CharacteristicDetails
            .AsNoTracking()
            .Where(d => d.Characteristic!.Name == CharacteristicName && d.Nombre == TypeIThresholdName)
            .Select(d => d.Valor)
            .FirstOrDefaultAsync();

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? new ObservationSettings(value)
            : Defaults;
    }

    public async Task<ObservationSettings> SaveAsync(ObservationSettings request)
    {
        if (request.TypeIThreshold is < 1 or > 50)
            throw new InvalidOperationException("Las situaciones Tipo I para la alerta deben estar entre 1 y 50.");

        var characteristic = await db.Characteristics.FirstOrDefaultAsync(c => c.Name == CharacteristicName);
        if (characteristic is null)
        {
            characteristic = new Characteristic { Name = CharacteristicName };
            db.Characteristics.Add(characteristic);
            await db.SaveChangesAsync();
        }

        var text = request.TypeIThreshold.ToString(CultureInfo.InvariantCulture);
        var detail = await db.CharacteristicDetails
            .FirstOrDefaultAsync(d => d.CharacteristicId == characteristic.CharacteristicId && d.Nombre == TypeIThresholdName);

        if (detail is null)
        {
            db.CharacteristicDetails.Add(new CharacteristicDetail
            {
                CharacteristicId = characteristic.CharacteristicId,
                Nombre = TypeIThresholdName,
                Valor = text,
            });
        }
        else
        {
            detail.Valor = text;
        }

        await db.SaveChangesAsync();
        return await GetAsync();
    }
}
