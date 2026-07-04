using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(IEventService events) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<EventResponse>>> List(
        [FromQuery] EventListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await events.ListAsync(query, ct));
    }

    [HttpGet("past-years")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<int>>> PastYears(CancellationToken ct)
    {
        return Ok(await events.GetPastYearsAsync(ct));
    }

    [HttpGet("{eventId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventResponse>> Get(Guid eventId, CancellationToken ct)
    {
        return ToOk(await events.GetByIdAsync(eventId, ct));
    }

    [HttpGet("categoryType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<EventCategoryTypeResponse>>> CategoryTypes(
        CancellationToken ct
    )
    {
        return Ok(await events.ListCategoryTypesAsync(ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> Create(
        [FromBody] CreateEventRequest request,
        CancellationToken ct
    )
    {
        return ToCreated(await events.CreateAsync(request, UserId, ct), e => $"/api/events/{e.Id}");
    }

    [HttpPut("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> Update(
        Guid eventId,
        [FromBody] UpdateEventRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await events.UpdateAsync(eventId, request, UserId, ct));
    }

    [HttpDelete("{eventId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> Delete(Guid eventId, CancellationToken ct)
    {
        return ToNoContent(await events.DeleteAsync(eventId, ct));
    }

    [HttpPatch("{eventId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> Feature(Guid eventId, CancellationToken ct)
    {
        return ToOk(await events.SetFeaturedAsync(eventId, ct));
    }

    [HttpPost("categoryType")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventCategoryTypeResponse>> CreateCategoryType(
        [FromBody] CreateEventCategoryTypeRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await events.CreateCategoryTypeAsync(request, ct));
    }

    [HttpPut("categoryType/{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventCategoryTypeResponse>> UpdateCategoryType(
        Guid eventCategoryTypeId,
        [FromBody] UpdateEventCategoryTypeRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await events.UpdateCategoryTypeAsync(eventCategoryTypeId, request, ct));
    }

    [HttpDelete("categoryType/{eventCategoryTypeId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteCategoryType(
        Guid eventCategoryTypeId,
        CancellationToken ct
    )
    {
        return ToNoContent(await events.DeleteCategoryTypeAsync(eventCategoryTypeId, ct));
    }
}
