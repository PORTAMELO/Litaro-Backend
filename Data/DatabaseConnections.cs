using System.Data.Common;
using Npgsql;

namespace Litaro.Data;

public static class DatabaseConnections
{
    public const int DefaultMaxPoolSize = 10;

    private static readonly HashSet<string> MaxPoolSizeKeys = ["maximumpoolsize", "maxpoolsize"];

    public static string? WithPoolLimit(string? connectionString, int maxPoolSize)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var raw = new DbConnectionStringBuilder { ConnectionString = connectionString };
        var hasLimit = raw.Keys.Cast<string>()
            .Any(key => MaxPoolSizeKeys.Contains(key.Replace(" ", string.Empty).ToLowerInvariant()));

        if (hasLimit)
            return connectionString;

        return new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize }.ConnectionString;
    }
}
