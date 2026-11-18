using Litaro.Data;
using Litaro.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Litaro.Tests.Pg;

public sealed class PgDatabase : IAsyncDisposable
{
    public const string EnvironmentVariable = "LITARO_TEST_PG";
    private const string NamePrefix = "litaro_test_";

    private static readonly AuditCommandInterceptor Interceptor = new();

    private readonly string serverConnectionString;
    private readonly string name;

    private PgDatabase(string serverConnectionString, string name, string connectionString)
    {
        this.serverConnectionString = serverConnectionString;
        this.name = name;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<PgDatabase> CreateAsync()
    {
        var server = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrEmpty(server))
        {
            throw new InvalidOperationException("LITARO_TEST_PG no está definida.");
        }

        var name = NamePrefix + Guid.NewGuid().ToString("N");
        var serverConnectionString = new NpgsqlConnectionStringBuilder(server) { Pooling = false }.ConnectionString;
        var connectionString = new NpgsqlConnectionStringBuilder(server) { Database = name, Pooling = false }.ConnectionString;

        await using (var connection = new NpgsqlConnection(serverConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        var database = new PgDatabase(serverConnectionString, name, connectionString);
        try
        {
            await using var db = database.NewContext();
            await db.Database.MigrateAsync();
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }

        return database;
    }

    public AppDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new AppDbContext(options);
    }

    public AppDbContext NewContext(AuditContext audit, int? maxPoolSize = null, int? maxBatchSize = null, IInterceptor? observer = null)
    {
        var connectionString = maxPoolSize is { } size ? PooledConnectionString(size) : ConnectionString;
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                if (maxBatchSize is { } batch)
                    npgsql.MaxBatchSize(batch);
            })
            .AddInterceptors(Interceptor);
        if (observer is not null)
            builder.AddInterceptors(observer);
        var options = builder.Options;

        return new AppDbContext(options, audit);
    }

    public string PooledConnectionString(int maxPoolSize) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = true, MaxPoolSize = maxPoolSize }.ConnectionString;

    public async ValueTask DisposeAsync()
    {
        if (!name.StartsWith(NamePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Solo se eliminan bases de datos que empiezan por {NamePrefix}.");
        }

        await using var connection = new NpgsqlConnection(serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
