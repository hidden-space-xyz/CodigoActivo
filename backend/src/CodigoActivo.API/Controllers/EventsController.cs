using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(IEventService events, IParticipationService participation)
    : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Events)]
    public async Task<ActionResult<PagedResult<EventListItemResponse>>> ListAsync(
        [FromQuery] EventListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await events.ListAsync(query, ct));
    }

    [HttpGet("past-years")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Events)]
    public async Task<ActionResult<IReadOnlyList<int>>> PastYearsAsync(CancellationToken ct)
    {
        return Ok(await events.GetPastYearsAsync(ct));
    }

    [HttpGet("{eventId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Events)]
    public async Task<ActionResult<EventResponse>> GetAsync(Guid eventId, CancellationToken ct)
    {
        return ToOk(await events.GetByIdAsync(eventId, ct));
    }

    [HttpGet("categoryType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventCategoryTypeResponse>>> CategoryTypesAsync(
        [FromQuery] EventCategoryTypeListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await events.ListCategoryTypesAsync(query, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> CreateAsync(
        [FromBody] CreateEventRequest request,
        CancellationToken ct
    )
    {
        return ToCreated(await events.CreateAsync(request, UserId, ct), e => $"/api/events/{e.Id}");
    }

    [HttpPut("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> UpdateAsync(
        Guid eventId,
        [FromBody] UpdateEventRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await events.UpdateAsync(eventId, request, UserId, ct));
    }

    [HttpDelete("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(Guid eventId, CancellationToken ct)
    {
        return ToNoContent(await events.DeleteAsync(eventId, ct));
    }

    [HttpPatch("{eventId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> FeatureAsync(Guid eventId, CancellationToken ct)
    {
        return ToOk(await events.SetFeaturedAsync(eventId, ct));
    }

    [HttpGet("{eventId:guid}/ratings")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<EventRatingListItemResponse>>> RatingsAsync(
        Guid eventId,
        [FromQuery] EventRatingListQuery query,
        CancellationToken ct
    )
    {
        return ToOk(await participation.ListEventRatingsAsync(eventId, query, ct));
    }

    [HttpPut("{eventId:guid}/rating")]
    [Authorize]
    public async Task<ActionResult<EventRatingResponse>> SaveRatingAsync(
        Guid eventId,
        [FromBody] SaveEventRatingRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await participation.SaveRatingAsync(eventId, UserId, request, ct));
    }

    [HttpPost("categoryType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventCategoryTypeResponse>> CreateCategoryTypeAsync(
        [FromBody] CreateEventCategoryTypeRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await events.CreateCategoryTypeAsync(request, ct));
    }

    [HttpPut("categoryType/{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventCategoryTypeResponse>> UpdateCategoryTypeAsync(
        Guid eventCategoryTypeId,
        [FromBody] UpdateEventCategoryTypeRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await events.UpdateCategoryTypeAsync(eventCategoryTypeId, request, ct));
    }

    [HttpDelete("categoryType/{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteCategoryTypeAsync(
        Guid eventCategoryTypeId,
        CancellationToken ct
    )
    {
        return ToNoContent(await events.DeleteCategoryTypeAsync(eventCategoryTypeId, ct));
    }
}
