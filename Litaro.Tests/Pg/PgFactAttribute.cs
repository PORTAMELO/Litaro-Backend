namespace Litaro.Tests.Pg;

public sealed class PgFactAttribute : FactAttribute
{
    public PgFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PgDatabase.EnvironmentVariable)))
        {
            Skip = "LITARO_TEST_PG no está definida.";
        }
    }
}
