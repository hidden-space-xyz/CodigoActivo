using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.Participation.Commands;
using CodigoActivo.Application.Participation.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing events.
/// </summary>
[ApiController]
[Route("api/events")]
public class EventsController : ApiControllerBase
{
    /// <summary>
    /// Lists the events that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged event list item, or an error response.</returns>
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Events)]
    public async Task<ActionResult<PagedResult<EventListItemResponse>>> ListAsync(
        [FromQuery] EventListQuery query,
        [FromServices] ListEventsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListEventsQuery(query), ct));
    }

    /// <summary>
    /// Executes the past years endpoint for events.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an int, or an error response.</returns>
    [HttpGet("past-years")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Events)]
    public async Task<ActionResult<IReadOnlyList<int>>> PastYearsAsync(
        [FromServices] GetPastEventYearsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetPastEventYearsQuery(), ct));
    }

    /// <summary>
    /// Lists the category types assigned to at least one past event.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the event category types, or an error response.</returns>
    [HttpGet("past-categories")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Events)]
    public async Task<ActionResult<IReadOnlyList<EventCategoryTypeResponse>>> PastCategoriesAsync(
        [FromServices] GetPastEventCategoryTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetPastEventCategoryTypesQuery(), ct));
    }

    /// <summary>
    /// Gets the requested event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event, or an error response.</returns>
    [HttpGet("{eventId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Events)]
    public async Task<ActionResult<EventResponse>> GetAsync(
        Guid eventId,
        [FromServices] GetEventByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventByIdQuery(eventId), ct));
    }

    /// <summary>
    /// Executes the category types endpoint for events.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged event category type, or an error response.</returns>
    [HttpGet("categoryType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventCategoryTypeResponse>>> CategoryTypesAsync(
        [FromQuery] EventCategoryTypeListQuery query,
        [FromServices] ListEventCategoryTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListEventCategoryTypesQuery(query), ct));
    }

    /// <summary>
    /// Executes the terms documents endpoint for events.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged terms document, or an error response.</returns>
    [HttpGet("termsDocument")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<TermsDocumentResponse>>> TermsDocumentsAsync(
        [FromQuery] TermsDocumentListQuery query,
        [FromServices] ListTermsDocumentsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListTermsDocumentsQuery(query), ct));
    }

    /// <summary>
    /// Executes the terms state endpoint for events, returning every document linked to the
    /// event along with the current user's decision for each one.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the event's terms state, or an error response.</returns>
    [HttpGet("{eventId:guid}/terms")]
    [Authorize]
    public async Task<ActionResult<EventTermsStateResponse>> TermsStateAsync(
        Guid eventId,
        [FromServices] GetEventTermsStateQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventTermsStateQuery(eventId, UserId), ct));
    }

    /// <summary>
    /// Lists the event's activities that the current user leads with a confirmed assignment and
    /// that have not ended yet, each with its currently confirmed attendees. Any other user,
    /// administrators included, receives an empty list.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the led activities with their attendees.</returns>
    [HttpGet("{eventId:guid}/leader-roster")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<LeaderRosterActivityResponse>>> LeaderRosterAsync(
        Guid eventId,
        [FromServices] GetLeaderRosterQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetLeaderRosterQuery(eventId, UserId), ct));
    }

    /// <summary>
    /// Creates an event from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> CreateAsync(
        [FromBody] CreateEventRequest request,
        [FromServices] CreateEventCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreateEventCommand(request, UserId), ct),
            e => $"/api/events/{e.Id}"
        );
    }

    /// <summary>
    /// Updates the selected event with the validated request.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event, or an error response.</returns>
    [HttpPut("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> UpdateAsync(
        Guid eventId,
        [FromBody] UpdateEventRequest request,
        [FromServices] UpdateEventCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdateEventCommand(eventId, request, UserId), ct)
        );
    }

    /// <summary>
    /// Deletes the selected event.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid eventId,
        [FromServices] DeleteEventCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteEventCommand(eventId), ct));
    }

    /// <summary>
    /// Executes the feature endpoint for events.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event, or an error response.</returns>
    [HttpPatch("{eventId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> FeatureAsync(
        Guid eventId,
        [FromServices] SetEventFeaturedCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new SetEventFeaturedCommand(eventId), ct));
    }

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
        [FromServices] ListEventRatingsQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new ListEventRatingsQuery(eventId, query), ct));
    }

    /// <summary>
    /// Executes the save rating endpoint for events. Each call stores one more anonymous rating, so
    /// an attendee may rate the same event as many times as they want.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("{eventId:guid}/rating")]
    [Authorize]
    public async Task<IActionResult> SaveRatingAsync(
        Guid eventId,
        [FromBody] SaveEventRatingRequest request,
        [FromServices] SaveEventRatingCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new SaveEventRatingCommand(eventId, UserId, request), ct)
        );
    }

    /// <summary>
    /// Creates a category type.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event category type, or an error response.</returns>
    [HttpPost("categoryType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventCategoryTypeResponse>> CreateCategoryTypeAsync(
        [FromBody] CreateEventCategoryTypeRequest request,
        [FromServices] CreateEventCategoryTypeCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new CreateEventCategoryTypeCommand(request), ct));
    }

    /// <summary>
    /// Updates a category type with the supplied data.
    /// </summary>
    /// <param name="eventCategoryTypeId">Identifier of the event category type.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event category type, or an error response.</returns>
    [HttpPut("categoryType/{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventCategoryTypeResponse>> UpdateCategoryTypeAsync(
        Guid eventCategoryTypeId,
        [FromBody] UpdateEventCategoryTypeRequest request,
        [FromServices] UpdateEventCategoryTypeCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new UpdateEventCategoryTypeCommand(eventCategoryTypeId, request),
                ct
            )
        );
    }

    /// <summary>
    /// Deletes a category type when it is no longer referenced.
    /// </summary>
    /// <param name="eventCategoryTypeId">Identifier of the event category type.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("categoryType/{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteCategoryTypeAsync(
        Guid eventCategoryTypeId,
        [FromServices] DeleteEventCategoryTypeCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteEventCategoryTypeCommand(eventCategoryTypeId), ct)
        );
    }

    /// <summary>
    /// Creates a terms document.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a terms document, or an error response.</returns>
    [HttpPost("termsDocument")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<TermsDocumentResponse>> CreateTermsDocumentAsync(
        [FromBody] CreateTermsDocumentRequest request,
        [FromServices] CreateTermsDocumentCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new CreateTermsDocumentCommand(request), ct));
    }

    /// <summary>
    /// Updates a terms document with the supplied data.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a terms document, or an error response.</returns>
    [HttpPut("termsDocument/{termsDocumentId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<TermsDocumentResponse>> UpdateTermsDocumentAsync(
        Guid termsDocumentId,
        [FromBody] UpdateTermsDocumentRequest request,
        [FromServices] UpdateTermsDocumentCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdateTermsDocumentCommand(termsDocumentId, request), ct)
        );
    }

    /// <summary>
    /// Deletes a terms document when it is no longer referenced.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("termsDocument/{termsDocumentId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteTermsDocumentAsync(
        Guid termsDocumentId,
        [FromServices] DeleteTermsDocumentCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteTermsDocumentCommand(termsDocumentId), ct)
        );
    }
}
