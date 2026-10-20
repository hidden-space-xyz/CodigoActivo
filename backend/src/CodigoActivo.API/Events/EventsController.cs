using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Events.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Activities.Contracts;
using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Common.Caching;
using CodigoActivo.Application.EventCategories.Contracts;
using CodigoActivo.Application.Events.Commands;
using CodigoActivo.Application.Events.Contracts;
using CodigoActivo.Application.Events.Queries;
using CodigoActivo.Application.TermsDecisions.Commands;
using CodigoActivo.Application.TermsDecisions.Contracts;
using CodigoActivo.Application.TermsDecisions.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using EventId = CodigoActivo.Domain.Events.EventId;

namespace CodigoActivo.API.Events;

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
        [FromServices] IQueryHandler<ListEventsQuery, PagedResult<EventListItemResponse>> handler,
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
        [FromServices] IQueryHandler<GetPastEventYearsQuery, IReadOnlyList<int>> handler,
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
        [FromServices]
            IQueryHandler<
            GetPastEventCategoryTypesQuery,
            IReadOnlyList<EventCategoryTypeResponse>
        > handler,
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
        [FromServices] IQueryHandler<GetEventByIdQuery, Result<EventResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetEventByIdQuery(EventId.From(eventId)), ct));
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
        [FromServices]
            IQueryHandler<GetEventTermsStateQuery, Result<EventTermsStateResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new GetEventTermsStateQuery(EventId.From(eventId)), ct)
        );
    }

    /// <summary>
    /// Lists the terms decisions taken about the event, latest first, telling which ones were
    /// signed.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of terms decisions, or an error response.</returns>
    [HttpGet("{eventId:guid}/terms/decisions")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventTermsDecisionResponse>>> TermsDecisionsAsync(
        Guid eventId,
        [FromQuery] EventTermsDecisionListQuery query,
        [FromServices]
            IQueryHandler<
            ListEventTermsDecisionsQuery,
            Result<PagedResult<EventTermsDecisionResponse>>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new ListEventTermsDecisionsQuery(EventId.From(eventId), query),
                ct
            )
        );
    }

    /// <summary>
    /// Withdraws a consent given about a terms document of the event for a person: the person
    /// themselves or their guardian withdraws it, and an administrator records a withdrawal asked
    /// for by email.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person the consent is about.</param>
    /// <param name="termsDocumentId">Identifier of the terms document, which must hold a consent.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An empty HTTP response, or an error response.</returns>
    [HttpPost("{eventId:guid}/terms/decisions/{userId:guid}/{termsDocumentId:guid}/withdraw")]
    [AllowOnlySelf]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> WithdrawTermsConsentAsync(
        Guid eventId,
        Guid userId,
        Guid termsDocumentId,
        [FromServices] ICommandHandler<WithdrawTermsConsentCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new WithdrawTermsConsentCommand(
                    EventId.From(eventId),
                    UserId.From(userId),
                    TermsDocumentId.From(termsDocumentId)
                ),
                ct
            )
        );
    }

    /// <summary>
    /// Gets the PNG image of the signature a person drew to accept a terms document of the event.
    /// It sets no cache policy, so it is answered <c>no-store</c> like any other API response.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="userId">Identifier of the person who accepted the document.</param>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the image, or an error response.</returns>
    [HttpGet("{eventId:guid}/terms/decisions/{userId:guid}/{termsDocumentId:guid}/signature")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> TermsSignatureAsync(
        Guid eventId,
        Guid userId,
        Guid termsDocumentId,
        [FromServices] IQueryHandler<GetEventTermsSignatureQuery, Result<byte[]>> handler,
        CancellationToken ct
    )
    {
        var result = await handler.HandleAsync(
            new GetEventTermsSignatureQuery(
                EventId.From(eventId),
                UserId.From(userId),
                TermsDocumentId.From(termsDocumentId)
            ),
            ct
        );
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        return File(result.Value, "image/png");
    }

    /// <summary>
    /// Lists the event's activities that the current user leads with a confirmed assignment and
    /// that have not ended yet, each with its currently confirmed attendees. Any other user,
    /// administrators included, receives an empty list.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the led activities with their attendees, or an error response.</returns>
    [HttpGet("{eventId:guid}/leader-roster")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<LeaderRosterActivityResponse>>> LeaderRosterAsync(
        Guid eventId,
        [FromServices]
            IQueryHandler<
            GetLeaderRosterQuery,
            Result<IReadOnlyList<LeaderRosterActivityResponse>>
        > handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetLeaderRosterQuery(EventId.From(eventId)), ct));
    }

    /// <summary>
    /// Creates an event from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EventResponse>> CreateAsync(
        [FromBody] CreateEventRequest request,
        [FromServices] ICommandHandler<CreateEventCommand, Result<EventId>> handler,
        [FromServices] IQueryHandler<GetEventByIdQuery, Result<EventResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(), ct),
            id => getById.HandleAsync(new GetEventByIdQuery(id), ct),
            id => $"/api/events/{id}"
        );
    }

    /// <summary>
    /// Updates the selected event with the validated request.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event, or an error response.</returns>
    [HttpPut("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> UpdateAsync(
        Guid eventId,
        [FromBody] UpdateEventRequest request,
        [FromServices] ICommandHandler<UpdateEventCommand, Result> handler,
        [FromServices] IQueryHandler<GetEventByIdQuery, Result<EventResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(EventId.From(eventId)), ct),
            () => getById.HandleAsync(new GetEventByIdQuery(EventId.From(eventId)), ct)
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
        [FromServices] ICommandHandler<DeleteEventCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteEventCommand(EventId.From(eventId)), ct)
        );
    }

    /// <summary>
    /// Executes the feature endpoint for events.
    /// </summary>
    /// <param name="eventId">Identifier of the event.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an event, or an error response.</returns>
    [HttpPatch("{eventId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> FeatureAsync(
        Guid eventId,
        [FromServices] ICommandHandler<SetEventFeaturedCommand, Result> handler,
        [FromServices] IQueryHandler<GetEventByIdQuery, Result<EventResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(new SetEventFeaturedCommand(EventId.From(eventId)), ct),
            () => getById.HandleAsync(new GetEventByIdQuery(EventId.From(eventId)), ct)
        );
    }
}
