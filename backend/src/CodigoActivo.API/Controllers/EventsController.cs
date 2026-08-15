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

[ApiController]
[Route("api/events")]
public class EventsController : ApiControllerBase
{
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

    [HttpGet("{eventId:guid}/terms-acceptance")]
    [Authorize]
    public async Task<ActionResult<EventTermsAcceptanceResponse>> TermsAcceptanceAsync(
        Guid eventId,
        [FromServices] GetEventTermsAcceptanceQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetEventTermsAcceptanceQuery(eventId, UserId), ct));
    }

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

    [HttpPut("{eventId:guid}/rating")]
    [Authorize]
    public async Task<ActionResult<EventRatingResponse>> SaveRatingAsync(
        Guid eventId,
        [FromBody] SaveEventRatingRequest request,
        [FromServices] SaveEventRatingCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new SaveEventRatingCommand(eventId, UserId, request), ct)
        );
    }

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
