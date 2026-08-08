using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Reports.Queries;

public sealed record GetEventBadgesQuery(Guid EventId) : IQuery<Result<EventBadgesResponse>>;

public sealed class GetEventBadgesQueryHandler(
    IEventRepository events,
    IActivityRepository activities,
    IQueryExecutor executor
) : IQueryHandler<GetEventBadgesQuery, Result<EventBadgesResponse>>
{
    public async Task<Result<EventBadgesResponse>> HandleAsync(
        GetEventBadgesQuery query,
        CancellationToken ct = default
    )
    {
        var eventId = query.EventId;
        var ev = await GetEventHeaderAsync(eventId, ct);
        if (ev is null)
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var rows = await executor.ToListAsync(
            activities
                .QueryAssignments()
                .Where(a =>
                    a.Activity.EventId == eventId
                    && a.AssignmentStatusId == SeedIds.AssignmentStatusTypes.Confirmed
                )
                .Select(a => new
                {
                    a.UserId,
                    a.User.FirstName,
                    a.User.LastName,
                    UserTypeName = a.User.UserType.Name,
                    UserTypeColor = a.User.UserType.Color,
                    a.User.CreatedAt,
                    Guardian = a.User.Parent == null
                        ? null
                        : new EventBadgeGuardianResponse(
                            a.User.Parent.FirstName,
                            a.User.Parent.LastName,
                            a.User.Parent.Phone
                        ),
                    a.ActivityId,
                    ActivityTitle = a.Activity.Title,
                    a.Activity.ActivityStartsAt,
                }),
            ct
        );

        var badges = rows.GroupBy(r => r.UserId)
            .Select(g =>
            {
                var user = g.First();
                return new EventBadgeResponse(
                    g.Key,
                    user.FirstName,
                    user.LastName,
                    user.UserTypeName,
                    user.UserTypeColor,
                    user.CreatedAt,
                    user.Guardian,
                    [
                        .. g.OrderBy(r => r.ActivityStartsAt)
                            .ThenBy(r => r.ActivityTitle, StringComparer.Ordinal)
                            .DistinctBy(r => r.ActivityId)
                            .Select(r => r.ActivityTitle),
                    ]
                );
            })
            .OrderBy(b => TextSearch.Normalize(b.LastName), StringComparer.Ordinal)
            .ThenBy(b => TextSearch.Normalize(b.FirstName), StringComparer.Ordinal)
            .ThenBy(b => b.UserId)
            .ToList();

        return new EventBadgesResponse(ev.Id, ev.Title, badges);
    }

    private Task<EventHeader?> GetEventHeaderAsync(Guid eventId, CancellationToken ct)
    {
        return executor.FirstOrDefaultAsync(
            events.Query().Where(e => e.Id == eventId).Select(e => new EventHeader(e.Id, e.Title)),
            ct
        );
    }

    private sealed record EventHeader(Guid Id, string Title);
}
