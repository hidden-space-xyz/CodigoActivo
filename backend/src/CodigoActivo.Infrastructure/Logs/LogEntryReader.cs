using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Localization;
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
/// entries get the names of the accounts and things they mention, one query per kind of thing on
/// the page, left out for those that no longer exist and for the consultations of the blocked data,
/// which the legal archive lists by their request.
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
        var related = page.Items.Select(view => RelatedOf(view.Entry.Related)).ToList();
        var names = await NamesAsync(
            page.Items.Select(view => MainEntityOf(view.Entry))
                .OfType<AuditEntityKey>()
                .Concat(related.SelectMany(entities => entities)),
            ct
        );
        return new PagedResult<AuditLogEntryResponse>(
            [.. page.Items.Select((view, index) => ToResponse(view, related[index], names))],
            page.Total,
            page.Page,
            page.PageSize
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountAuditEntryResponse>> ListAccountAuditAsync(
        Guid userId,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(subjectIds);

        var ids = subjectIds.ToList();
        var source = context
            .Set<AuditLogRow>()
            .Where(entry =>
                (entry.ActorType == AuditActorType.User && entry.ActorId == userId)
                || (
                    entry.EntityType == AuditEntityType.User
                    && entry.EntityId != null
                    && ids.Contains(entry.EntityId.Value)
                )
            );
        foreach (var related in ids.Select(id => JsonSerializer.Serialize(new[] { new { id } })))
        {
            source = source.Union(
                context
                    .Set<AuditLogRow>()
                    .Where(entry => EF.Functions.JsonContains(entry.Related!, related))
            );
        }

        var entries = await executor.ToListAsync(
            source
                .OrderBy(entry => entry.OccurredAt)
                .ThenBy(entry => entry.Id)
                .Select(entry => new
                {
                    entry.OccurredAt,
                    entry.Action,
                    entry.Outcome,
                    entry.ActorType,
                    entry.ActorId,
                    entry.ClientIp,
                }),
            ct
        );
        return
        [
            .. entries.Select(entry =>
            {
                var ranBySelf = entry.ActorType == AuditActorType.User && entry.ActorId == userId;
                var ranByAnother = entry.ActorType == AuditActorType.User && !ranBySelf;
                return new AccountAuditEntryResponse(
                    entry.OccurredAt,
                    entry.Action,
                    entry.Outcome,
                    entry.ActorType,
                    ranBySelf,
                    ranByAnother ? null : entry.ClientIp?.ToString()
                );
            }),
        ];
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

    private async Task<Dictionary<AuditEntityKey, string>> NamesAsync(
        IEnumerable<AuditEntityKey> entities,
        CancellationToken ct
    )
    {
        var names = new Dictionary<AuditEntityKey, string>();
        foreach (var kind in entities.GroupBy(entity => entity.Type, entity => entity.Id))
        {
            if (NamesOf(kind.Key, [.. kind.Distinct()]) is not { } source)
            {
                continue;
            }

            foreach (var named in await executor.ToListAsync(source, ct))
            {
                names[new AuditEntityKey(kind.Key, named.Id)] = named.Name;
            }
        }

        return names;
    }

    private IQueryable<NamedEntity>? NamesOf(AuditEntityType type, List<Guid> ids)
    {
        return type switch
        {
            AuditEntityType.Activity => context
                .Activities.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Title)),
            AuditEntityType.BlockedDataConsultation => null,
            AuditEntityType.Event => context
                .Events.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Title)),
            AuditEntityType.EventCategoryType => context
                .EventCategoryTypes.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Name)),
            AuditEntityType.LegalPolicy => context
                .LegalPolicies.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(
                    row.Id,
                    row.Kind == LegalPolicyKind.Privacy
                        ? AppStrings.LegalPoliciesPrivacy
                        : AppStrings.LegalPoliciesCookies
                )),
            AuditEntityType.NewsItem => context
                .News.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Title)),
            AuditEntityType.Partner => context
                .Partners.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Name)),
            AuditEntityType.Resource => context
                .Resources.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Title)),
            AuditEntityType.StoredFile => context
                .Files.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Name)),
            AuditEntityType.TermsDocument => context
                .TermsDocuments.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.Name)),
            AuditEntityType.User => context
                .Users.Where(row => ids.Contains(row.Id))
                .Select(row => new NamedEntity(row.Id, row.FirstName + " " + row.LastName)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static AuditEntityKey? MainEntityOf(AuditLogRow entry)
    {
        return entry is { EntityType: { } type, EntityId: { } id }
            ? new AuditEntityKey(type, id)
            : null;
    }

    private static AuditLogEntryResponse ToResponse(
        AuditLogView view,
        List<AuditEntityKey> related,
        Dictionary<AuditEntityKey, string> names
    )
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
            MainEntityOf(entry) is { } main ? names.GetValueOrDefault(main) : null,
            [
                .. related.Select(entity => new AuditRelatedEntityResponse(
                    entity.Type,
                    entity.Id,
                    names.GetValueOrDefault(entity)
                )),
            ],
            entry.Outcome,
            entry.ErrorCode,
            entry.TraceId
        );
    }

    private static List<AuditEntityKey> RelatedOf(string? json)
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
                .Select(related => new AuditEntityKey(related.Type, related.Id)),
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

    private readonly record struct AuditEntityKey(AuditEntityType Type, Guid Id);

    private sealed record NamedEntity(Guid Id, string Name);

    private sealed class AuditLogView
    {
        public AuditLogRow Entry { get; init; } = null!;

        public string? ActorName { get; init; }

        public string? ActorEmail { get; init; }

        public string? EntityName { get; init; }

        public string? EntityEmail { get; init; }
    }
}
