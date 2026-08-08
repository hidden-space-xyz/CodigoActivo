using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Announcements.Commands;
using CodigoActivo.Application.Announcements.Queries;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/announcements")]
public class AnnouncementsController : ApiControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<PagedResult<AnnouncementListItemResponse>>> ListAsync(
        [FromQuery] AnnouncementListQuery query,
        [FromServices] ListAnnouncementsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListAnnouncementsQuery(query), ct));
    }

    [HttpGet("years")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<IReadOnlyList<int>>> YearsAsync(
        [FromServices] GetAnnouncementYearsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetAnnouncementYearsQuery(), ct));
    }

    [HttpGet("{announcementId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.Announcements)]
    public async Task<ActionResult<AnnouncementResponse>> GetAsync(
        Guid announcementId,
        [FromServices] GetAnnouncementByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetAnnouncementByIdQuery(announcementId), ct));
    }

    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> CreateAsync(
        [FromBody] CreateAnnouncementRequest request,
        [FromServices] CreateAnnouncementCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreateAnnouncementCommand(request, UserId), ct),
            a => $"/api/announcements/{a.Id}"
        );
    }

    [HttpPut("{announcementId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> UpdateAsync(
        Guid announcementId,
        [FromBody] UpdateAnnouncementRequest request,
        [FromServices] UpdateAnnouncementCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(
                new UpdateAnnouncementCommand(announcementId, request, UserId),
                ct
            )
        );
    }

    [HttpDelete("{announcementId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid announcementId,
        [FromServices] DeleteAnnouncementCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteAnnouncementCommand(announcementId), ct)
        );
    }

    [HttpPatch("{announcementId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<AnnouncementResponse>> FeatureAsync(
        Guid announcementId,
        [FromServices] SetAnnouncementFeaturedCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new SetAnnouncementFeaturedCommand(announcementId), ct)
        );
    }
}
