using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Announcements.Queries;

/// <summary>
/// Carries the criteria used to retrieve announcement by identifier.
/// </summary>
/// <param name="AnnouncementId">Identifier of the announcement.</param>
public sealed record GetAnnouncementByIdQuery(Guid AnnouncementId)
    : IQuery<Result<AnnouncementResponse>>;

/// <summary>
/// Executes the query to retrieve announcement by identifier.
/// </summary>
/// <param name="announcements">Repository used to persist and retrieve announcements.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="cache">Cache used to reuse previously computed results.</param>
public sealed class GetAnnouncementByIdQueryHandler(
    IAnnouncementRepository announcements,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetAnnouncementByIdQuery, Result<AnnouncementResponse>>
{
    /// <summary>
    /// Handles the request to retrieve announcement by identifier.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an announcement on success, or an application error on failure.</returns>
    public Task<Result<AnnouncementResponse>> HandleAsync(
        GetAnnouncementByIdQuery query,
        CancellationToken ct = default
    )
    {
        return cache.GetEntityAsync(
            executor,
            $"announcements:id:{query.AnnouncementId}",
            () =>
                announcements
                    .Query()
                    .Where(a => a.Id == query.AnnouncementId)
                    .Select(Projections.Announcement),
            CacheTags.Announcements,
            ErrorCode.AnnouncementNotFound,
            ct
        );
    }
}
