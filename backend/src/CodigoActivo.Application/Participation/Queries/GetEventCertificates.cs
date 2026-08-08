using System.Globalization;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Participation.Queries;

public sealed record GetEventCertificatesQuery(Guid UserId)
    : IQuery<IReadOnlyList<EventCertificateResponse>>;

public sealed class GetEventCertificatesQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetEventCertificatesQuery, IReadOnlyList<EventCertificateResponse>>
{
    private const string CertificateCodePrefix = "CA";
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    public async Task<IReadOnlyList<EventCertificateResponse>> HandleAsync(
        GetEventCertificatesQuery query,
        CancellationToken ct = default
    )
    {
        var userId = query.UserId;
        var today = clock.Today;

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    (a.UserId == userId || a.User.ParentId == userId)
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                    && a.Activity.Event.EventEndsAt < today
                )
                .Select(a => new CertificateRow
                {
                    EventId = a.Activity.EventId,
                    EventTitle = a.Activity.Event.Title,
                    EventSubtitle = a.Activity.Event.Subtitle,
                    EventStartsAt = a.Activity.Event.EventStartsAt,
                    EventEndsAt = a.Activity.Event.EventEndsAt,
                    UserId = a.UserId,
                    FirstName = a.User.FirstName,
                    LastName = a.User.LastName,
                }),
            ct
        );

        return
        [
            .. rows.DistinctBy(row => (row.EventId, row.UserId))
                .OrderByDescending(row => row.EventEndsAt)
                .ThenBy(row => row.EventId)
                .ThenByDescending(row => row.UserId == userId)
                .ThenBy(row => TextSearch.Normalize(row.FirstName), StringComparer.Ordinal)
                .ThenBy(row => TextSearch.Normalize(row.LastName), StringComparer.Ordinal)
                .ThenBy(row => row.UserId)
                .Select(row => new EventCertificateResponse(
                    BuildCertificateCode(row.EventId, row.UserId, row.EventEndsAt.Year),
                    row.EventId,
                    row.UserId,
                    row.FirstName,
                    row.LastName,
                    row.UserId == userId,
                    row.EventTitle,
                    row.EventSubtitle,
                    row.EventStartsAt,
                    row.EventEndsAt
                )),
        ];
    }

    private static string BuildCertificateCode(Guid eventId, Guid userId, int year)
    {
        Span<byte> eventBytes = stackalloc byte[16];
        Span<byte> userBytes = stackalloc byte[16];
        eventId.TryWriteBytes(eventBytes);
        userId.TryWriteBytes(userBytes);

        var hash = FnvOffsetBasis;
        unchecked
        {
            for (var i = 0; i < eventBytes.Length; i++)
            {
                hash = (hash ^ eventBytes[i]) * FnvPrime;
                hash = (hash ^ userBytes[i]) * FnvPrime;
            }
        }

        var digest = hash.ToString("X16", CultureInfo.InvariantCulture);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{CertificateCodePrefix}-{year:D4}-{digest[8..]}"
        );
    }

    private sealed record CertificateRow
    {
        public Guid EventId { get; init; }
        public string EventTitle { get; init; } = string.Empty;
        public string EventSubtitle { get; init; } = string.Empty;
        public DateOnly EventStartsAt { get; init; }
        public DateOnly EventEndsAt { get; init; }
        public Guid UserId { get; init; }
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
    }
}
