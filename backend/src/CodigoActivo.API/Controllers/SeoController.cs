using System.Text;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Seo.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace CodigoActivo.API.Controllers;

/// <summary>
/// Exposes HTTP endpoints for querying and managing seo.
/// </summary>
[ApiController]
[Route("api")]
[ApiExplorerSettings(IgnoreApi = true)]
public class SeoController : ApiControllerBase
{
    private static readonly TimeSpan ClientCacheLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Executes the sitemap endpoint for seo.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpGet("sitemap.xml")]
    [HttpHead("sitemap.xml")]
    [AllowAnonymous]
    [OutputCache(PolicyName = OutputCachePolicies.Seo)]
    public async Task<IActionResult> SitemapAsync(
        [FromServices] GetSitemapXmlQueryHandler handler,
        CancellationToken ct
    )
    {
        SetPublicCacheControl();
        return Content(
            await handler.HandleAsync(new GetSitemapXmlQuery(), ct),
            "application/xml",
            Encoding.UTF8
        );
    }

    /// <summary>
    /// Executes the robots endpoint for seo.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpGet("robots.txt")]
    [HttpHead("robots.txt")]
    [AllowAnonymous]
    [OutputCache(PolicyName = OutputCachePolicies.Seo)]
    public async Task<IActionResult> RobotsAsync(
        [FromServices] GetRobotsTxtQueryHandler handler,
        CancellationToken ct
    )
    {
        SetPublicCacheControl();
        return Content(
            await handler.HandleAsync(new GetRobotsTxtQuery(), ct),
            "text/plain",
            Encoding.UTF8
        );
    }

    private void SetPublicCacheControl()
    {
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = ClientCacheLifetime,
        };
    }
}
