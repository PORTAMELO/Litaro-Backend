using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Litaro.Data;
using Litaro.Models;
using Microsoft.EntityFrameworkCore;

namespace Litaro.Services;

public record AuditQuery(
    string Category,
    DateOnly? From,
    DateOnly? To,
    int? UserId,
    string? EventType,
    string? TableName,
    string? RecordId,
    Guid? OperationId,
    int Page = 1,
    int PageSize = 50);

public record AuditSummaryDto(
    long Id,
    DateTime OccurredAt,
    string EventType,
    string? TableName,
    string TableLabel,
    string? RecordId,
    string ActorKind,
    int? UserId,
    string Actor,
    string? Source,
    string? IpAddress,
    IReadOnlyList<string> ChangedLabels,
    string? Reason,
    string? DocumentType,
    string? FileName,
    string? EntityLabel,
    bool Moved,
    string? MovedFromLabel = null);

public record AuditPage(IReadOnlyList<AuditSummaryDto> Items, bool HasMore);

public record AuditChangeDto(string Column, string Label, string? Before, string? After, bool Masked, bool BeforeMasked, bool AfterMasked, bool Changed = false);

public record AuditDetailDto(
    AuditSummaryDto Summary,
    string DbUser,
    string? UserAgent,
    Guid? OperationId,
    IReadOnlyList<AuditChangeDto> Changes,
    IReadOnlyList<AuditChangeDto> AllFields,
    AuditSummaryDto? PreviousDocument,
    IReadOnlyList<AuditSummaryDto> Related);

public record AuditOptionDto(string Value, string Label);

public record AuditUserOptionDto(int Id, string Name);

public class AuditQueryService(AppDbContext db)
{
    private const int MaxRangeDays = 93;
    private const int MaxPage = 10000;
    private static readonly DateOnly MinQueryDate = new(2000, 1, 1);
    private static readonly DateOnly MaxQueryDate = new(2100, 12, 31);
    private const int MaxPageSize = 100;
    private const int UserSearchLimit = 20;
    private const int RelatedLimit = 100;
    private const string MaskedValue = "***";
    private const string SystemWithoutContext = "Litaro (sin contexto)";
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);
    private static readonly string[] SecretColumns = ["PasswordHash", "SecurityStamp", "ConcurrencyStamp"];

    private static readonly string[] DataEvents =
        [AuditEventTypes.Insert, AuditEventTypes.Update, AuditEventTypes.Delete];

    private static readonly string[] AccessEvents =
        [AuditEventTypes.LoginOk, AuditEventTypes.LoginFail, AuditEventTypes.Logout];

    private static readonly string[] DocumentEvents =
    [
        AuditEventTypes.DocUpload, AuditEventTypes.DocUpdate, AuditEventTypes.DocReplace,
        AuditEventTypes.DocRemove, AuditEventTypes.DocPurge,
    ];

    private static readonly Dictionary<string, string> EventLabels = new()
    {
        [AuditEventTypes.Insert] = "Creó",
        [AuditEventTypes.Update] = "Editó",
        [AuditEventTypes.Delete] = "Eliminó",
        [AuditEventTypes.LoginOk] = "Ingreso",
        [AuditEventTypes.LoginFail] = "Ingreso no completado",
        [AuditEventTypes.Logout] = "Salida",
        [AuditEventTypes.DocUpload] = "Carga",
        [AuditEventTypes.DocUpdate] = "Modificación o traslado",
        [AuditEventTypes.DocReplace] = "Reemplazo",
        [AuditEventTypes.DocRemove] = "Retiro",
        [AuditEventTypes.DocPurge] = "Purga de archivo",
    };

    private sealed class SummaryRow
    {
        public long AuditLogId { get; set; }
        public DateTime OccurredAt { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? TableName { get; set; }
        public string? RecordId { get; set; }
        public string ActorKind { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public string DbUser { get; set; } = string.Empty;
        public string? Source { get; set; }
        public string? IpAddress { get; set; }
        public string[]? ChangedColumns { get; set; }
        public string? Detail { get; set; }
    }

    private static readonly Expression<Func<AuditLog, SummaryRow>> ToSummaryRow = a => new SummaryRow
    {
        AuditLogId = a.AuditLogId,
        OccurredAt = a.OccurredAt,
        EventType = a.EventType,
        TableName = a.TableName,
        RecordId = a.RecordId,
        ActorKind = a.ActorKind,
        UserId = a.UserId,
        UserName = a.UserName,
        DbUser = a.DbUser,
        Source = a.Source,
        IpAddress = a.IpAddress,
        ChangedColumns = a.ChangedColumns,
        Detail = a.Detail,
    };

    public async Task<AuditPage> ListAsync(AuditQuery q, DateTime nowUtc)
    {
        var events = q.Category switch
        {
            "data" => DataEvents,
            "access" => AccessEvents,
            "document" => DocumentEvents,
            _ => throw new InvalidOperationException("La categoría de consulta no es válida."),
        };

        var today = DateOnly.FromDateTime(nowUtc.Add(ColombiaOffset));
        var to = q.To ?? today;
        var from = q.From ?? (to > MinQueryDate.AddDays(6) ? to.AddDays(-6) : MinQueryDate);
        if (from < MinQueryDate || to > MaxQueryDate)
            throw new InvalidOperationException("Las fechas de consulta no son válidas.");
        if (from > to)
            throw new InvalidOperationException("La fecha inicial no puede ser posterior a la final.");
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
            throw new InvalidOperationException("El rango máximo de consulta es de 93 días.");

        var fromUtc = StartOfDayUtc(from);
        var toUtc = StartOfDayUtc(to.AddDays(1));
        var page = Math.Max(q.Page, 1);
        if (page > MaxPage)
            throw new InvalidOperationException("La página solicitada excede el límite de consulta.");
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);

        var query = db.AuditLogs.AsNoTracking()
            .Where(a => events.Contains(a.EventType) && a.OccurredAt >= fromUtc && a.OccurredAt < toUtc);

        if (q.UserId is { } userId)
            query = query.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(q.EventType))
        {
            var eventType = q.EventType.Trim();
            query = query.Where(a => a.EventType == eventType);
        }
        if (!string.IsNullOrWhiteSpace(q.TableName))
        {
            var tableName = q.TableName.Trim();
            query = query.Where(a => a.TableName == tableName);
        }
        if (!string.IsNullOrWhiteSpace(q.RecordId))
        {
            var recordId = q.RecordId.Trim();
            query = query.Where(a => a.RecordId == recordId);
        }
        if (q.OperationId is { } operationId)
            query = query.Where(a => a.OperationId == operationId);

        var rows = await query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.AuditLogId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize + 1)
            .Select(ToSummaryRow)
            .ToListAsync();

        var hasMore = rows.Count > pageSize;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        var aliases = await AliasesAsync(rows);
        return new AuditPage(rows.Select(r => ToSummary(r, aliases)).ToList(), hasMore);
    }

    public async Task<AuditDetailDto?> GetAsync(long id)
    {
        var row = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a => a.AuditLogId == id);
        if (row is null)
            return null;

        var summaryRow = ToSummaryRow.Compile()(row);
        var oldJson = ParseObject(row.OldData);
        var newJson = ParseObject(row.NewData);
        var detail = ParseObject(row.Detail);

        var related = row.OperationId is { } operationId
            ? await db.AuditLogs.AsNoTracking()
                .Where(a => a.OperationId == operationId && a.AuditLogId != id)
                .OrderBy(a => a.OccurredAt)
                .ThenBy(a => a.AuditLogId)
                .Take(RelatedLimit)
                .Select(ToSummaryRow)
                .ToListAsync()
            : [];

        SummaryRow? previous = null;
        if (row.EventType == AuditEventTypes.DocReplace && row.OperationId is { } opId)
        {
            var candidates = await db.AuditLogs.AsNoTracking()
                .Where(a => a.OperationId == opId && a.EventType == AuditEventTypes.DocReplaced && a.AuditLogId < id)
                .OrderByDescending(a => a.AuditLogId)
                .Select(ToSummaryRow)
                .ToListAsync();
            previous = candidates.FirstOrDefault(c => SameDocumentKey(detail, ParseObject(c.Detail)));
        }

        var keys = new List<string>();
        foreach (var json in new[] { newJson, oldJson })
        {
            if (json is null)
                continue;
            foreach (var key in json.Select(p => p.Key))
                if (!keys.Contains(key))
                    keys.Add(key);
        }

        var all = related.Append(summaryRow);
        if (previous is not null)
            all = all.Append(previous);
        var aliases = await AliasesAsync(all, keys);

        var changedSet = ChangedSet(row, keys, oldJson, newJson);
        var updated = row.EventType == AuditEventTypes.Update ? changedSet : [];
        var allFields = keys.Select(k => ToChange(row.TableName, k, oldJson, newJson, aliases, updated.Contains(k))).ToList();
        var changes = (oldJson is null || newJson is null ? keys : keys.Where(changedSet.Contains))
            .Select(k => ToChange(row.TableName, k, oldJson, newJson, aliases, updated.Contains(k)))
            .ToList();

        return new AuditDetailDto(
            ToSummary(summaryRow, aliases),
            row.DbUser,
            row.UserAgent,
            row.OperationId,
            changes,
            allFields,
            previous is null ? null : ToSummary(previous, aliases),
            related.Select(r => ToSummary(r, aliases)).ToList());
    }

    public async Task<List<AuditUserOptionDto>> SearchUsersAsync(string q)
    {
        var text = string.Join(' ', (q ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLower();
        if (text.Length < 2)
            return [];

        return await db.Users
            .AsNoTracking()
            .Where(u => u.FirstName.ToLower().Contains(text) ||
                        u.LastName.ToLower().Contains(text) ||
                        (u.FirstName + " " + u.LastName).ToLower().Contains(text) ||
                        u.DocumentNumber.ToLower().Contains(text) ||
                        (u.UserName != null && u.UserName.ToLower().Contains(text)))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Take(UserSearchLimit)
            .Select(u => new AuditUserOptionDto(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync();
    }

    public object Options() => new
    {
        tables = AuditLabels.Tables
            .Where(t => t.Key is not ("Auth" or "Document"))
            .Select(t => new AuditOptionDto(t.Key, t.Value))
            .OrderBy(t => t.Label, StringComparer.CurrentCulture)
            .ToList(),
        eventTypes = new
        {
            data = EventOptions(DataEvents),
            access = EventOptions(AccessEvents),
            document = EventOptions(DocumentEvents),
        },
    };

    private static List<AuditOptionDto> EventOptions(IEnumerable<string> types) =>
        types.Select(t => new AuditOptionDto(t, EventLabels[t])).ToList();

    private static DateTime StartOfDayUtc(DateOnly day) =>
        DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - ColombiaOffset, DateTimeKind.Utc);

    private async Task<Dictionary<(string Table, string Column), string>> AliasesAsync(
        IEnumerable<SummaryRow> rows,
        IEnumerable<string>? extraColumns = null)
    {
        var list = rows.ToList();
        var tables = list.Select(r => r.TableName).OfType<string>().Distinct().ToList();
        var columns = list.SelectMany(r => r.ChangedColumns ?? []).Concat(extraColumns ?? []).Distinct().ToList();
        if (tables.Count == 0 || columns.Count == 0)
            return [];

        var found = await db.ColumnConfigurations.AsNoTracking()
            .Where(c => c.Alias != null && c.Alias != "" && tables.Contains(c.TableName) && columns.Contains(c.ColumnName))
            .Select(c => new { c.TableName, c.ColumnName, c.Alias })
            .ToListAsync();

        var result = new Dictionary<(string Table, string Column), string>();
        foreach (var c in found)
            result.TryAdd((c.TableName, c.ColumnName), c.Alias!);
        return result;
    }

    private static string ColumnLabel(
        Dictionary<(string Table, string Column), string> aliases, string? table, string column) =>
        table is not null && aliases.TryGetValue((table, column), out var alias) ? alias : column;

    private static AuditSummaryDto ToSummary(SummaryRow r, Dictionary<(string Table, string Column), string> aliases)
    {
        var detail = ParseObject(r.Detail);
        var entityTable = ValueText(detail?["EntityTable"]);
        var entityId = ValueText(detail?["EntityId"]);
        var isDocument = r.TableName == "Document";
        var movedFrom = isDocument ? detail?["movedFrom"] as JsonObject : null;
        var movedFromTable = ValueText(movedFrom?["EntityTable"]);

        return new AuditSummaryDto(
            r.AuditLogId,
            r.OccurredAt,
            r.EventType,
            r.TableName,
            AuditLabels.Table(r.TableName),
            r.RecordId,
            r.ActorKind,
            r.UserId,
            ActorText(r),
            r.Source,
            r.IpAddress,
            (r.ChangedColumns ?? []).Select(c => ColumnLabel(aliases, r.TableName, c)).ToList(),
            r.TableName == "Auth" ? ValueText(detail?["reason"]) : null,
            isDocument ? ValueText(detail?["DocumentType"]) : null,
            isDocument ? ValueText(detail?["FileName"]) : null,
            isDocument && entityTable is not null ? $"{AuditLabels.Table(entityTable)} #{entityId}" : null,
            movedFrom is not null,
            movedFromTable is not null ? $"{AuditLabels.Table(movedFromTable)} #{ValueText(movedFrom?["EntityId"])}" : null);
    }

    private static string ActorText(SummaryRow r) => r.ActorKind switch
    {
        AuditActorKinds.System => $"Sistema · {(string.IsNullOrEmpty(r.Source) ? SystemWithoutContext : r.Source)}",
        AuditActorKinds.DirectDb => $"BD directa ({r.DbUser})",
        AuditActorKinds.Anonymous => string.IsNullOrEmpty(r.UserName) ? "Anónimo" : r.UserName,
        _ => !string.IsNullOrEmpty(r.UserName)
            ? r.UserName
            : r.UserId is { } id ? $"Usuario #{id}" : "Usuario",
    };

    private static HashSet<string> ChangedSet(AuditLog row, List<string> keys, JsonObject? oldJson, JsonObject? newJson)
    {
        if (row.ChangedColumns is { } columns)
            return columns.ToHashSet();
        if (oldJson is null || newJson is null)
            return [];

        return keys.Where(k => RawOf(oldJson[k]) != RawOf(newJson[k])).ToHashSet();
    }

    private static AuditChangeDto ToChange(
        string? table,
        string column,
        JsonObject? oldJson,
        JsonObject? newJson,
        Dictionary<(string Table, string Column), string> aliases,
        bool changed)
    {
        var label = ColumnLabel(aliases, table, column);
        var before = oldJson?[column];
        var after = newJson?[column];

        var secret = SecretColumns.Contains(column);
        var beforeMasked = secret && IsMask(before);
        var afterMasked = secret && IsMask(after);

        return new AuditChangeDto(
            column,
            label,
            beforeMasked ? null : ValueText(before),
            afterMasked ? null : ValueText(after),
            beforeMasked || afterMasked,
            beforeMasked,
            afterMasked,
            changed);
    }

    private static bool IsMask(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String &&
        value.GetValue<string>() == MaskedValue;

    private static string? ValueText(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        JsonValue value when value.GetValueKind() == JsonValueKind.Null => null,
        _ => node.ToJsonString(),
    };

    private static string? RawOf(JsonNode? node) => node?.ToJsonString();

    private static bool SameDocumentKey(JsonObject? current, JsonObject? previous) =>
        current is not null && previous is not null &&
        new[] { "DocumentType", "EntityTable", "EntityId" }.All(k => RawOf(current[k]) == RawOf(previous[k]));

    private static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
