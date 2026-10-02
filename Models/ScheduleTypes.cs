namespace Litaro.Models
{
    public static class ScheduleTypes
    {
        public const string Class = "CLASS";
        public const string ParentAttention = "PARENT_ATTENTION";
        public const string Accompaniment = "ACCOMPANIMENT";
        public const string Meeting = "MEETING";
        public const string Other = "OTHER";

        public static readonly string[] All = [Class, ParentAttention, Accompaniment, Meeting, Other];
    }
}
