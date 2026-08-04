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
[Route("api/announcements")]
public class AnnouncementsController(IAnnouncementService announcements) : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<PagedResult<AnnouncementListItemResponse>>> ListAsync(
        [FromQuery] AnnouncementListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await announcements.ListAsync(query, ct));
    }

    [HttpGet("years")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<IReadOnlyList<int>>> YearsAsync(CancellationToken ct)
    {
        return Ok(await announcements.GetYearsAsync(ct));
    }

    [HttpGet("{announcementId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<AnnouncementResponse>> GetAsync(
        Guid announcementId,
        CancellationToken ct
    )
    {
        return ToOk(await announcements.GetByIdAsync(announcementId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> CreateAsync(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken ct
    )
    {
        return ToCreated(
            await announcements.CreateAsync(request, UserId, ct),
            a => $"/api/announcements/{a.Id}"
        );
    }

    [HttpPut("{announcementId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> UpdateAsync(
        Guid announcementId,
        [FromBody] UpdateAnnouncementRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await announcements.UpdateAsync(announcementId, request, UserId, ct));
    }

    [HttpDelete("{announcementId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(Guid announcementId, CancellationToken ct)
    {
        return ToNoContent(await announcements.DeleteAsync(announcementId, ct));
    }

    [HttpPatch("{announcementId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> FeatureAsync(
        Guid announcementId,
        CancellationToken ct
    )
    {
        return ToOk(await announcements.SetFeaturedAsync(announcementId, ct));
    }
}
