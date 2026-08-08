using System.Text;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Seo.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api")]
[ApiExplorerSettings(IgnoreApi = true)]
public class SeoController : ApiControllerBase
{
    private static readonly TimeSpan ClientCacheLifetime = TimeSpan.FromHours(1);

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
