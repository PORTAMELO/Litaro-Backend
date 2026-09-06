namespace Litaro.Models;

public class Characteristic
{
    public int CharacteristicId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}
