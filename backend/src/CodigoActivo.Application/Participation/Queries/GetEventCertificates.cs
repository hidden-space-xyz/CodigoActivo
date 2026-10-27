using System.Globalization;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.Participation.Queries;

/// <summary>
/// Carries the request for the attendance certificates of the signed-in user and their dependents.
/// </summary>
public sealed record GetEventCertificatesQuery : IQuery<IReadOnlyList<EventCertificateResponse>>;

/// <summary>
/// Executes the query for the attendance certificates of the signed-in user and their dependents:
/// one per person and event whose last day has passed, for every event in which the person had a
/// confirmed signup.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="currentUser">Person the query runs for.</param>
public sealed class GetEventCertificatesQueryHandler(
    IReadStore readStore,
    IQueryExecutor executor,
    IClock clock,
    ICurrentUser currentUser
) : IQueryHandler<GetEventCertificatesQuery, IReadOnlyList<EventCertificateResponse>>
{
    private const string CertificateCodePrefix = "CA";
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>
    /// Handles the request for the attendance certificates of the household.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the certificates, the most recently ended event first and,
    /// within an event, the signed-in user before their dependents.
    /// </returns>
    public async Task<IReadOnlyList<EventCertificateResponse>> HandleAsync(
        GetEventCertificatesQuery query,
        CancellationToken ct = default
    )
    {
        var userId = currentUser.RequiredId().Value;
        var today = clock.Today;

        var rows = await executor.ToListAsync(
            readStore
                .Assignments.Where(ActivityProjections.ConfirmedSignup)
                .Where(a =>
                    (a.UserId == userId || a.User.ParentId == userId)
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
        /// Gets or sets the first day of the event.
        /// </summary>
        public DateOnly EventStartsAt { get; init; }

        /// <summary>
        /// Gets or sets the last day of the event.
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
