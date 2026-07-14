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
    public async Task<ActionResult<PagedResult<AnnouncementListItemResponse>>> List(
        [FromQuery] AnnouncementListQuery query,
        CancellationToken ct
    )
    {
        return Ok(await announcements.ListAsync(query, ct));
    }

    [HttpGet("years")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<IReadOnlyList<int>>> Years(CancellationToken ct)
    {
        return Ok(await announcements.GetYearsAsync(ct));
    }

    [HttpGet("{announcementId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<AnnouncementResponse>> Get(
        Guid announcementId,
        CancellationToken ct
    )
    {
        return ToOk(await announcements.GetByIdAsync(announcementId, ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> Create(
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
