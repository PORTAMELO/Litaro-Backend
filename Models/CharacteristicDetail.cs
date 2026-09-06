namespace Litaro.Models;

public class CharacteristicDetail
{
    public int CharacteristicDetailId { get; set; }

    public int CharacteristicId { get; set; }

    public Characteristic? Characteristic { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Valor { get; set; } = string.Empty;

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}
