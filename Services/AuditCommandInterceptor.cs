using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Litaro.Services;

public sealed class AuditCommandInterceptor : DbCommandInterceptor, IDbTransactionInterceptor
{
    private const string ParameterPrefix = "__audit_";

    private static readonly string[] Settings =
    [
        AuditSettings.App,
        AuditSettings.ActorKind,
        AuditSettings.UserId,
        AuditSettings.UserName,
        AuditSettings.Source,
        AuditSettings.Ip,
        AuditSettings.UserAgent,
        AuditSettings.OperationId,
        AuditSettings.Action,
    ];

    private static readonly string SetConfigSql =
        "SELECT " + string.Join(", ", Settings.Select((name, i) => $"set_config('{name}', @{ParameterPrefix}{i}, true)"));

    private readonly ConditionalWeakTable<DbTransaction, Applied> applied = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Apply(command, eventData);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, eventData, cancellationToken);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Apply(command, eventData);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, eventData, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        if (!TryPrefix(command, eventData))
            Apply(command, eventData);

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!TryPrefix(command, eventData))
            await ApplyAsync(command, eventData, cancellationToken);

        return result;
    }

    public void RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData) => applied.Remove(transaction);

    public Task RolledBackToSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken = default)
    {
        applied.Remove(transaction);
        return Task.CompletedTask;
    }

    private static string[]? PendingValues(DbCommand command, CommandEventData eventData)
    {
        if ((eventData.Context as AppDbContext)?.Audit is not { } audit || !Writes(command.CommandText))
            return null;

        return
        [
            "on",
            audit.ActorKind,
            audit.UserId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            audit.UserName ?? string.Empty,
            audit.Source ?? string.Empty,
            audit.IpAddress ?? string.Empty,
            audit.UserAgent ?? string.Empty,
            audit.OperationId.ToString(),
            audit.Action ?? string.Empty,
        ];
    }

    private static bool Writes(string sql) =>
        sql.Contains("INSERT INTO", StringComparison.Ordinal)
        || sql.Contains("UPDATE ", StringComparison.Ordinal)
        || sql.Contains("DELETE FROM", StringComparison.Ordinal);

    private static bool TryPrefix(DbCommand command, CommandEventData eventData)
    {
        if (command.Transaction is not null || PendingValues(command, eventData) is not { } values)
            return false;

        if (command.Parameters.Cast<DbParameter>().Any(p => string.IsNullOrEmpty(p.ParameterName)))
            return true;

        command.CommandText = SetConfigSql + ";\n" + command.CommandText;
        AddParameters(command, values);
        return true;
    }

    private void Apply(DbCommand command, CommandEventData eventData)
    {
        if (CreateSetCommand(command, eventData) is not { } pending)
            return;

        var (set, transaction, signature) = pending;

        using (set)
        {
            set.ExecuteNonQuery();
        }

        applied.AddOrUpdate(transaction, signature);
    }

    private async Task ApplyAsync(DbCommand command, CommandEventData eventData, CancellationToken cancellationToken)
    {
        if (CreateSetCommand(command, eventData) is not { } pending)
            return;

        var (set, transaction, signature) = pending;

        await using (set)
        {
            await set.ExecuteNonQueryAsync(cancellationToken);
        }

        applied.AddOrUpdate(transaction, signature);
    }

    private (DbCommand Set, DbTransaction Transaction, Applied Signature)? CreateSetCommand(DbCommand command, CommandEventData eventData)
    {
        if (command.Transaction is not { } transaction || PendingValues(command, eventData) is not { } values)
            return null;

        var signature = new Applied(eventData.Context?.Database.CurrentTransaction?.TransactionId, values);
        if (applied.TryGetValue(transaction, out var last) && last.Matches(signature))
            return null;

        var set = command.Connection!.CreateCommand();
        set.Transaction = transaction;
        set.CommandText = SetConfigSql;
        AddParameters(set, values);
        return (set, transaction, signature);
    }

    private static void AddParameters(DbCommand command, string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = ParameterPrefix + i;
            parameter.DbType = DbType.String;
            parameter.Value = values[i];
            command.Parameters.Add(parameter);
        }
    }

    private sealed record Applied(Guid? TransactionId, string[] Values)
    {
        public bool Matches(Applied other) =>
            TransactionId is not null
            && TransactionId == other.TransactionId
            && Values.AsSpan().SequenceEqual(other.Values);
    }
}
