using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Emails.Queries;

/// <summary>
/// Carries the criteria used to preview who an email to event attendees would reach.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Filters">The same attendee filters the send endpoint receives.</param>
public sealed record GetEventAttendeesEmailAudienceQuery(
    Guid EventId,
    EventAttendeeListQuery Filters
) : IQuery<Result<EmailAudienceResponse>>;

/// <summary>
/// Executes the query that previews who an email to event attendees would reach, selecting
/// recipients exactly as <see cref="Commands.SendEmailToEventAttendeesCommandHandler"/> does.
/// </summary>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventAttendeesEmailAudienceQueryHandler(
    IUserRepository users,
    IEventRepository events,
    IQueryExecutor executor
) : IQueryHandler<GetEventAttendeesEmailAudienceQuery, Result<EmailAudienceResponse>>
{
    /// <summary>
    /// Handles the request to preview the audience of an email to event attendees.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the audience summary, or an application error on failure.</returns>
    public async Task<Result<EmailAudienceResponse>> HandleAsync(
        GetEventAttendeesEmailAudienceQuery query,
        CancellationToken ct = default
    )
    {
        if (!await events.ExistsAsync(e => e.Id == query.EventId, ct))
        {
            return Error.NotFound(ErrorCode.EventNotFound);
        }

        var audience = await ManualEmailAudience.LoadAsync(
            UserFilters.ApplyEventAttendees(users.Query(), query.EventId, query.Filters),
            executor,
            ct
        );
        return audience.ToResponse();
    }
}
