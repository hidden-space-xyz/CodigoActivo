using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.News.Commands;
using CodigoActivo.Application.News.Queries;
using CodigoActivo.Application.Querying;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing news items.
/// </summary>
[ApiController]
[Route("api/news")]
public class NewsController : ApiControllerBase
{
    /// <summary>
    /// Lists the news items that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged news list item, or an error response.</returns>
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.News)]
    public async Task<ActionResult<PagedResult<NewsListItemResponse>>> ListAsync(
        [FromQuery] NewsListQuery query,
        [FromServices] ListNewsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListNewsQuery(query), ct));
    }

    /// <summary>
    /// Executes the years endpoint for news items.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an int, or an error response.</returns>
    [HttpGet("years")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.News)]
    public async Task<ActionResult<IReadOnlyList<int>>> YearsAsync(
        [FromServices] GetNewsYearsQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetNewsYearsQuery(), ct));
    }

    /// <summary>
    /// Gets the requested news item.
    /// </summary>
    /// <param name="newsItemId">Identifier of the news item.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a news item, or an error response.</returns>
    [HttpGet("{newsItemId:guid}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CacheTags.News)]
    public async Task<ActionResult<NewsItemResponse>> GetAsync(
        Guid newsItemId,
        [FromServices] GetNewsItemByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetNewsItemByIdQuery(newsItemId), ct));
    }

    /// <summary>
    /// Creates a news item from the validated request.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a news item, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    public async Task<ActionResult<NewsItemResponse>> CreateAsync(
        [FromBody] CreateNewsItemRequest request,
        [FromServices] CreateNewsItemCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToCreated(
            await handler.HandleAsync(new CreateNewsItemCommand(request, UserId), ct),
            a => $"/api/news/{a.Id}"
        );
    }

    /// <summary>
    /// Updates the selected news item with the validated request.
    /// </summary>
    /// <param name="newsItemId">Identifier of the news item.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a news item, or an error response.</returns>
    [HttpPut("{newsItemId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<NewsItemResponse>> UpdateAsync(
        Guid newsItemId,
        [FromBody] UpdateNewsItemRequest request,
        [FromServices] UpdateNewsItemCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(
            await handler.HandleAsync(new UpdateNewsItemCommand(newsItemId, request, UserId), ct)
        );
    }

    /// <summary>
    /// Deletes the selected news item.
    /// </summary>
    /// <param name="newsItemId">Identifier of the news item.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{newsItemId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteAsync(
        Guid newsItemId,
        [FromServices] DeleteNewsItemCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteNewsItemCommand(newsItemId), ct));
    }

    /// <summary>
    /// Executes the feature endpoint for news items.
    /// </summary>
    /// <param name="newsItemId">Identifier of the news item.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a news item, or an error response.</returns>
    [HttpPatch("{newsItemId:guid}/feature")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<NewsItemResponse>> FeatureAsync(
        Guid newsItemId,
        [FromServices] SetNewsItemFeaturedCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new SetNewsItemFeaturedCommand(newsItemId), ct));
    }
}
