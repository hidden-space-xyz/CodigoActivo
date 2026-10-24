using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Participation.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Participation.Contracts;
using CodigoActivo.Application.Participation.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Participation;

/// <summary>
/// Exposes HTTP endpoints for the ratings attendees give to the events they took part in. Each
/// account keeps one rating per event; administrators read them without their authors.
/// </summary>
[ApiController]
[Route("api/events")]
[Tags("Events")]
public class EventRatingsController : ApiControllerBase
{
    /// <summary>
    /// Executes the ratings endpoint for events.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged event rating list item, or an error response.</returns>
    [HttpGet("{eventId:guid}/ratings")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventRatingListItemResponse>>> RatingsAsync(
        Guid eventId,
        [FromQuery] EventRatingListQuery query,
        [FromServices]
            IQueryHandler<
            ListEventRatingsQuery,
            Result<PagedResult<EventRatingListItemResponse>>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new ListEventRatingsQuery(EventId.From(eventId), query), ct)
        );
    }

    /// <summary>
    /// Executes the save rating endpoint for events. The first call stores the signed-in account's
    /// rating of the event and later ones replace it.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPut("{eventId:guid}/rating")]
    [Authorize]
    public async Task<IActionResult> SaveRatingAsync(
        Guid eventId,
        [FromBody] SaveEventRatingRequest request,
        [FromServices] ICommandHandler<SaveEventRatingCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(EventId.From(eventId)), ct));
    }

    /// <summary>
    /// Executes the delete rating endpoint for events: removes the signed-in account's rating of
    /// the event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{eventId:guid}/rating")]
    [Authorize]
    public async Task<IActionResult> DeleteRatingAsync(
        Guid eventId,
        [FromServices] ICommandHandler<DeleteEventRatingCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteEventRatingCommand(EventId.From(eventId)), ct)
        );
    }
}
