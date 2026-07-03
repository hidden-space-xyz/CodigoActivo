using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/announcements")]
public class AnnouncementCommandsController(IAnnouncementService announcements)
    : CommandControllerBase
{
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> Create(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken ct
    )
    {
        var result = await announcements.CreateAsync(request, UserId, ct);
        if (result.IsFailure) return ToProblem(result.Error!);

        return Created($"/api/odata/Announcements({result.Value.Id})", result.Value);
    }

    [HttpPut("{announcementId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> Update(
        Guid announcementId,
        [FromBody] UpdateAnnouncementRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await announcements.UpdateAsync(announcementId, request, UserId, ct));
    }

    [HttpDelete("{announcementId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> Delete(Guid announcementId, CancellationToken ct)
    {
        return ToNoContent(await announcements.DeleteAsync(announcementId, ct));
    }

    [HttpPatch("{announcementId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> Feature(
        Guid announcementId,
        CancellationToken ct
    )
    {
        return ToOk(await announcements.SetFeaturedAsync(announcementId, ct));
    }
}