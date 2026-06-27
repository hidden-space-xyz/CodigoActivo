using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController(IEventService events) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [Cached(nameof(Event))]
    public async Task<ActionResult<IReadOnlyList<EventResponse>>> List(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        CancellationToken ct
    )
    {
        return Ok(await events.ListAsync(startDate, endDate, ct));
    }

    [HttpGet("{eventId:guid}")]
    [AllowAnonymous]
    [Cached(nameof(Event))]
    public async Task<ActionResult<EventResponse>> GetById(Guid eventId, CancellationToken ct)
    {
        return ToOk(await events.GetByIdAsync(eventId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Event), nameof(DashboardSummaryResponse))]
    public async Task<ActionResult<EventResponse>> Create(
        [FromBody] CreateEventRequest request,
        CancellationToken ct
    )
    {
        var result = await events.CreateAsync(request, UserId, ct);
        return result.IsFailure
            ? (ActionResult<EventResponse>)ToProblem(result.Error!)
            : (ActionResult<EventResponse>)
                CreatedAtAction(nameof(GetById), new { eventId = result.Value.Id }, result.Value);
    }

    [HttpPut("{eventId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Event))]
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
    [InvalidatesCache(nameof(Event), nameof(Activity), nameof(DashboardSummaryResponse))]
    public async Task<IActionResult> Delete(Guid eventId, CancellationToken ct)
    {
        return ToNoContent(await events.DeleteAsync(eventId, ct));
    }

    [HttpPatch("{eventId:guid}/feature")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Event))]
    public async Task<ActionResult<EventResponse>> Feature(Guid eventId, CancellationToken ct)
    {
        return ToOk(await events.SetFeaturedAsync(eventId, ct));
    }
}
