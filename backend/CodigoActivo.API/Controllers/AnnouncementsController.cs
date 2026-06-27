using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/announcements")]
public class AnnouncementsController(IAnnouncementService announcements) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [Cached(nameof(Announcement))]
    public async Task<ActionResult<IReadOnlyList<AnnouncementResponse>>> List(CancellationToken ct)
    {
        return Ok(await announcements.ListAsync(ct));
    }

    [HttpGet("{announcementId:guid}")]
    [AllowAnonymous]
    [Cached(nameof(Announcement))]
    public async Task<ActionResult<AnnouncementResponse>> GetById(
        Guid announcementId,
        CancellationToken ct
    )
    {
        return ToOk(await announcements.GetByIdAsync(announcementId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Announcement), nameof(DashboardSummaryResponse))]
    public async Task<ActionResult<AnnouncementResponse>> Create(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken ct
    )
    {
        var result = await announcements.CreateAsync(request, UserId, ct);
        return result.IsFailure
            ? (ActionResult<AnnouncementResponse>)ToProblem(result.Error!)
            : (ActionResult<AnnouncementResponse>)
                CreatedAtAction(
                    nameof(GetById),
                    new { announcementId = result.Value.Id },
                    result.Value
                );
    }

    [HttpPut("{announcementId:guid}")]
    [AllowOnlyAdmin]
    [InvalidatesCache(nameof(Announcement))]
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
    [InvalidatesCache(nameof(Announcement), nameof(DashboardSummaryResponse))]
    public async Task<IActionResult> Delete(Guid announcementId, CancellationToken ct)
    {
        return ToNoContent(await announcements.DeleteAsync(announcementId, ct));
    }
}
