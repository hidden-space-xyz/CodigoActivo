using System.Globalization;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Participation.Queries;

/// <summary>
/// Carries the criteria used to retrieve event certificates.
/// </summary>
/// <param name="UserId">Identifier of the user.</param>
public sealed record GetEventCertificatesQuery(Guid UserId)
    : IQuery<IReadOnlyList<EventCertificateResponse>>;

/// <summary>
/// Executes the query to retrieve event certificates.
/// </summary>
/// <param name="activities">Repository used to persist and retrieve activities.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class GetEventCertificatesQueryHandler(
    IActivityRepository activities,
    IQueryExecutor executor,
    IClock clock
) : IQueryHandler<GetEventCertificatesQuery, IReadOnlyList<EventCertificateResponse>>
{
    private const string CertificateCodePrefix = "CA";
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>
    /// Handles the request to retrieve event certificates.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the matching event certificate items.</returns>
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
        /// <summary>
        /// Gets or sets the identifier of the associated event.
        /// </summary>
        public Guid EventId { get; init; }
        /// <summary>
        /// Gets or sets the event title value.
        /// </summary>
        public string EventTitle { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the event subtitle value.
        /// </summary>
        public string EventSubtitle { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the date and time when the event starts.
        /// </summary>
        public DateOnly EventStartsAt { get; init; }
        /// <summary>
        /// Gets or sets the date and time when the event ends.
        /// </summary>
        public DateOnly EventEndsAt { get; init; }
        /// <summary>
        /// Gets or sets the identifier of the associated user.
        /// </summary>
        public Guid UserId { get; init; }
        /// <summary>
        /// Gets or sets the first name value.
        /// </summary>
        public string FirstName { get; init; } = string.Empty;
        /// <summary>
        /// Gets or sets the last name value.
        /// </summary>
        public string LastName { get; init; } = string.Empty;
    }
}
