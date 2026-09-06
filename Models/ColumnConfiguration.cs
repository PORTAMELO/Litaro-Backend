namespace Litaro.Models
{
    public class ColumnConfiguration
    {
        public int ColumnConfigurationId { get; set; }

        public string TableName { get; set; } = string.Empty;

        public string ColumnName { get; set; } = string.Empty;

        public bool Filterable { get; set; }

        public bool Visible { get; set; } = true;

        public string? Alias { get; set; }

        public int? CharacteristicId { get; set; }

        public Characteristic? Characteristic { get; set; }

        public DateTime CreationDate { get; set; }
    }
}