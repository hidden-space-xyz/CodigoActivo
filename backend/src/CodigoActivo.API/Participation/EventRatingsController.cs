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
    /// Lists the ratings of an event without identifying their authors.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of ratings, or an error response.</returns>
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
    /// Saves the signed-in account's rating of the event: the first call stores it and later ones
    /// replace it. Only an event that has ended and in which someone of the household had a
    /// confirmed signup can be rated.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
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
    /// Removes the signed-in account's rating of the event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
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
