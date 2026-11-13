using Litaro.Data;
using Npgsql;

namespace Litaro.Tests;

public class DatabaseConnectionsTests
{
    [Fact]
    public void WithPoolLimit_adds_the_limit_when_missing()
    {
        var result = DatabaseConnections.WithPoolLimit("Host=db;Port=5432;Database=postgres", 10);

        Assert.Equal(10, new NpgsqlConnectionStringBuilder(result).MaxPoolSize);
    }

    [Fact]
    public void WithPoolLimit_keeps_an_explicit_limit()
    {
        var result = DatabaseConnections.WithPoolLimit("Host=db;Port=5432;Maximum Pool Size=4", 10);

        Assert.Equal(4, new NpgsqlConnectionStringBuilder(result).MaxPoolSize);
    }

    [Fact]
    public void WithPoolLimit_keeps_the_rest_of_the_connection_string()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConnections.WithPoolLimit("Host=db;Port=6543;Database=postgres;Username=u", 10));

        Assert.Equal("db", result.Host);
        Assert.Equal(6543, result.Port);
        Assert.Equal("postgres", result.Database);
        Assert.Equal("u", result.Username);
    }

    [Fact]
    public void WithPoolLimit_leaves_an_empty_string_empty()
    {
        Assert.Null(DatabaseConnections.WithPoolLimit(null, 10));
    }
}
