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

/// <summary>
/// Exposes HTTP endpoints for querying and managing announcements.
/// </summary>
[ApiController]
[Route("api/announcements")]
public class AnnouncementsController : ApiControllerBase
{
    /// <summary>
    /// Lists the announcements that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged announcement list item, or an error response.</returns>
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

    /// <summary>
    /// Executes the years endpoint for announcements.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an int, or an error response.</returns>
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

    /// <summary>
    /// Gets the requested announcement.
    /// </summary>
    /// <param name="announcementId">Identifier of the announcement.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an announcement, or an error response.</returns>
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

    /// <summary>
    /// Creates an announcement from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an announcement, or an error response.</returns>
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

    /// <summary>
    /// Updates the selected announcement with the validated request.
    /// </summary>
    /// <param name="announcementId">Identifier of the announcement.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an announcement, or an error response.</returns>
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

    /// <summary>
    /// Deletes the selected announcement.
    /// </summary>
    /// <param name="announcementId">Identifier of the announcement.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
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

    /// <summary>
    /// Executes the feature endpoint for announcements.
    /// </summary>
    /// <param name="announcementId">Identifier of the announcement.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an announcement, or an error response.</returns>
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
