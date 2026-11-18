namespace Litaro.Tests.Pg;

public sealed class PgDatabaseFixture : IAsyncLifetime
{
    private PgDatabase? database;

    public PgDatabase Database => database ?? throw new InvalidOperationException("LITARO_TEST_PG no está definida.");

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(PgDatabase.EnvironmentVariable)))
        {
            return;
        }

        database = await PgDatabase.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }
}
