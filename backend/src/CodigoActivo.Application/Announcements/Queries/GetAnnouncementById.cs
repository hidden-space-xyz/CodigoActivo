using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace CodigoActivo.Application.Announcements.Queries;

public sealed record GetAnnouncementByIdQuery(Guid AnnouncementId)
    : IQuery<Result<AnnouncementResponse>>;

public sealed class GetAnnouncementByIdQueryHandler(
    IAnnouncementRepository announcements,
    IQueryExecutor executor,
    HybridCache cache
) : IQueryHandler<GetAnnouncementByIdQuery, Result<AnnouncementResponse>>
{
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
