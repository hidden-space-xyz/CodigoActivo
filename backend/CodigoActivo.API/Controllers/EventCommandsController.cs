using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/events")]
public class EventCommandsController(IEventService events) : ApiControllerBase
{
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<EventResponse>> Create(
        [FromBody] CreateEventRequest request,
        CancellationToken ct
    )
    {
        var result = await events.CreateAsync(request, UserId, ct);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        return Created($"/api/odata/Events({result.Value.Id})", result.Value);
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
}
