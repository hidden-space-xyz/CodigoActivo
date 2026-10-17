using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Logs;
using CodigoActivo.Application.Logs.Contracts;
using CodigoActivo.Infrastructure.Database.Context;
using CodigoActivo.Infrastructure.Database.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Reads the log tables through the read context. Every filter lands on an index: the trigram
/// index of the message for its text search, the containment index of the related entities and
/// the B-tree indexes of the other columns; a period also prunes the daily partitions. Audit
/// entries get the names of the users they mention from <c>users</c>, left out when the account
/// no longer exists.
/// </summary>
/// <param name="context">Read-side database context.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class LogEntryReader(CodigoActivoReadDbContext context, IQueryExecutor executor)
    : ILogEntryReader
{
    private static readonly SortMap<DiagnosticLogRow> DiagnosticSort =
        new SortMap<DiagnosticLogRow>()
            .Add("occurredAt", entry => entry.OccurredAt)
            .Add("level", entry => entry.Level)
            .Default("-occurredAt")
            .Tie(entry => entry.Id);

    private static readonly SortMap<AuditLogView> AuditSort = new SortMap<AuditLogView>()
        .Add("occurredAt", view => view.Entry.OccurredAt)
        .Add("action", view => view.Entry.Action)
        .Default("-occurredAt")
        .Tie(view => view.Entry.Id);

    /// <inheritdoc />
    public Task<PagedResult<DiagnosticLogEntryResponse>> ListDiagnosticsAsync(
        DiagnosticLogEntryListQuery criteria,
        LogPeriod period,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(period);

        var source = context.Set<DiagnosticLogRow>().AsQueryable();
        if (period.From is { } from)
        {
            source = source.Where(entry => entry.OccurredAt >= from);
        }

        if (period.Until is { } until)
        {
            source = source.Where(entry => entry.OccurredAt < until);
        }

        if (criteria.Level is { } level)
        {
            source = source.Where(entry => entry.Level == level);
        }

        if (Containing(criteria.Search) is { } message)
        {
            source = source.Where(entry =>
                EF.Functions.ILike(entry.Message, message, LikePattern.EscapeCharacter)
            );
        }

        if (Containing(criteria.Category) is { } category)
        {
            source = source.Where(entry =>
                EF.Functions.ILike(entry.Category, category, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(entry.EventName!, category, LikePattern.EscapeCharacter)
            );
        }

        if (Trace(criteria.TraceId) is { } trace)
        {
            source = source.Where(entry => entry.TraceId == trace);
        }

        return executor.ToPagedAsync(
            DiagnosticSort
                .Apply(source, criteria.Sort)
                .Select(entry => new DiagnosticLogEntryResponse(
                    entry.Id,
                    entry.OccurredAt,
                    entry.Level,
                    entry.Category,
                    entry.EventName,
                    entry.Message,
                    entry.Exception != null,
                    entry.TraceId
                )),
            criteria.Page,
            criteria.PageSize,
            ct
        );
    }

    /// <inheritdoc />
    public Task<DiagnosticLogEntryDetailResponse?> FindDiagnosticAsync(
        Guid id,
        CancellationToken ct = default
    )
    {
        if (id.Version != 7)
        {
            return Task.FromResult<DiagnosticLogEntryDetailResponse?>(null);
        }

        var from = InstantOf(id);
        var until = from.AddMilliseconds(1);
        return executor.FirstOrDefaultAsync(
            context
                .Set<DiagnosticLogRow>()
                .Where(entry =>
                    entry.OccurredAt >= from && entry.OccurredAt < until && entry.Id == id
                )
                .Select(entry => new DiagnosticLogEntryDetailResponse(
                    entry.Id,
                    entry.OccurredAt,
                    entry.Level,
                    entry.Category,
                    entry.EventName,
                    entry.Message,
                    entry.Exception,
                    entry.Properties,
                    entry.TraceId
                )),
            ct
        );
    }

    /// <inheritdoc />
    public async Task<PagedResult<AuditLogEntryResponse>> ListAuditAsync(
        AuditLogEntryListQuery criteria,
        LogPeriod period,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(period);

        var entries = AuditEntries(criteria, period);
        var source =
            from entry in entries
            from actor in context.Users.Where(user => user.Id == entry.ActorId).DefaultIfEmpty()
            from target in context
                .Users.Where(user =>
                    entry.EntityType == AuditEntityType.User && user.Id == entry.EntityId
                )
                .DefaultIfEmpty()
            select new AuditLogView
            {
                Entry = entry,
                ActorName = actor == null ? null : actor.FirstName + " " + actor.LastName,
                ActorEmail = actor == null ? null : actor.Email,
                EntityName = target == null ? null : target.FirstName + " " + target.LastName,
                EntityEmail = target == null ? null : target.Email,
            };

        if (Containing(criteria.Search) is { } person)
        {
            source = source.Where(view =>
                EF.Functions.ILike(view.ActorName!, person, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(view.ActorEmail!, person, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(view.EntityName!, person, LikePattern.EscapeCharacter)
                || EF.Functions.ILike(view.EntityEmail!, person, LikePattern.EscapeCharacter)
            );
        }

        var page = await executor.ToPagedAsync(
            AuditSort.Apply(source, criteria.Sort),
            criteria.Page,
            criteria.PageSize,
            ct
        );
        return new PagedResult<AuditLogEntryResponse>(
            [.. page.Items.Select(ToResponse)],
            page.Total,
            page.Page,
            page.PageSize
        );
    }

    private IQueryable<AuditLogRow> AuditEntries(AuditLogEntryListQuery criteria, LogPeriod period)
    {
        var source = context.Set<AuditLogRow>().AsQueryable();
        if (period.From is { } from)
        {
            source = source.Where(entry => entry.OccurredAt >= from);
        }

        if (period.Until is { } until)
        {
            source = source.Where(entry => entry.OccurredAt < until);
        }

        if (criteria.ActorType is { } actorType)
        {
            source = source.Where(entry => entry.ActorType == actorType);
        }

        if (criteria.ActorId is { } actorId)
        {
            source = source.Where(entry => entry.ActorId == actorId);
        }

        if (criteria.Action is { } action)
        {
            source = source.Where(entry => entry.Action == action);
        }

        if (criteria.EntityType is { } entityType)
        {
            source = source.Where(entry => entry.EntityType == entityType);
        }

        if (criteria.EntityId is { } entityId)
        {
            var related = JsonSerializer.Serialize(new[] { new { id = entityId } });
            source = source.Where(entry =>
                entry.EntityId == entityId || EF.Functions.JsonContains(entry.Related!, related)
            );
        }

        if (criteria.Outcome is { } outcome)
        {
            source = source.Where(entry => entry.Outcome == outcome);
        }

        if (criteria.ClientIp is { } clientIp && IPAddress.TryParse(clientIp, out var address))
        {
            source = source.Where(entry => entry.ClientIp == address);
        }

        if (Trace(criteria.TraceId) is { } trace)
        {
            source = source.Where(entry => entry.TraceId == trace);
        }

        return source;
    }

    private static AuditLogEntryResponse ToResponse(AuditLogView view)
    {
        var entry = view.Entry;
        return new AuditLogEntryResponse(
            entry.Id,
            entry.OccurredAt,
            entry.ActorType,
            entry.ActorId,
            view.ActorName,
            view.ActorEmail,
            entry.ClientIp?.ToString(),
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            view.EntityName,
            RelatedOf(entry.Related),
            DetailsOf(entry.Details),
            entry.Outcome,
            entry.ErrorCode,
            entry.ItemCount,
            entry.TraceId
        );
    }

    private static List<AuditRelatedEntityResponse> RelatedOf(string? json)
    {
        if (json is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return
        [
            .. document
                .RootElement.EnumerateArray()
                .Select(element =>
                    (
                        Valid: Enum.TryParse<AuditEntityType>(
                            element.GetProperty("type").GetString(),
                            out var type
                        ),
                        Type: type,
                        Id: element.GetProperty("id").GetGuid()
                    )
                )
                .Where(related => related.Valid)
                .Select(related => new AuditRelatedEntityResponse(related.Type, related.Id)),
        ];
    }

    private static List<AuditDetailResponse> DetailsOf(string? json)
    {
        if (json is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        return
        [
            .. document
                .RootElement.EnumerateObject()
                .Select(property => new AuditDetailResponse(
                    property.Name,
                    property.Value.ValueKind switch
                    {
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => property.Value.ToString(),
                    }
                ))
                .OrderBy(detail => detail.Name, StringComparer.Ordinal),
        ];
    }

    private static string? Containing(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : $"%{LikePattern.Literal(text.Trim())}%";
    }

    private static string? Trace(string? traceId)
    {
        return string.IsNullOrWhiteSpace(traceId) ? null : traceId.Trim().ToLowerInvariant();
    }

    private static DateTimeOffset InstantOf(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes, bigEndian: true, out _);
        var milliseconds = (long)(BinaryPrimitives.ReadUInt64BigEndian(bytes) >> 16);
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private sealed class AuditLogView
    {
        public AuditLogRow Entry { get; init; } = null!;

        public string? ActorName { get; init; }

        public string? ActorEmail { get; init; }

        public string? EntityName { get; init; }

        public string? EntityEmail { get; init; }
    }
}
