using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Emails.Contracts;
using CodigoActivo.Application.Reports.Contracts;
using CodigoActivo.Application.Users;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;

namespace CodigoActivo.Application.Emails.Queries;

/// <summary>
/// Carries the criteria used to preview who an email to event attendees would reach.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="Filters">The same attendee filters the send endpoint receives.</param>
public sealed record GetEventAttendeesEmailAudienceQuery(
    EventId EventId,
    EventAttendeeListQuery Filters
) : IQuery<Result<EmailAudienceResponse>>;

/// <summary>
/// Executes the query that previews who an email to event attendees would reach, selecting
/// recipients exactly as <see cref="Commands.SendEmailToEventAttendeesCommandHandler"/> does.
/// </summary>
/// <param name="readStore">Read side the query reads from.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
public sealed class GetEventAttendeesEmailAudienceQueryHandler(
    IReadStore readStore,
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
        ArgumentNullException.ThrowIfNull(query);
        var eventId = query.EventId.Value;
        if (!await executor.AnyAsync(readStore.Events.Where(e => e.Id == eventId), ct))
        {
            return Error.NotFound(ApplicationErrorCode.EventNotFound);
        }

        var audience = await ManualEmailAudience.LoadAsync(
            UserFilters.ApplyEventAttendees(readStore.Users, eventId, query.Filters),
            executor,
            ct
        );
        return audience.ToResponse();
    }
}
