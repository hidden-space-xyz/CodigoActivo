using System.Text;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Caching;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api")]
[ApiExplorerSettings(IgnoreApi = true)]
public class SeoController(ISitemapService sitemap) : ApiControllerBase
{
    private static readonly TimeSpan ClientCacheLifetime = TimeSpan.FromHours(1);

    [HttpGet("sitemap.xml")]
    [HttpHead("sitemap.xml")]
    [AllowAnonymous]
    [OutputCache(PolicyName = OutputCachePolicies.Seo)]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        SetPublicCacheControl();
        return Content(await sitemap.GetSitemapXmlAsync(ct), "application/xml", Encoding.UTF8);
    }

    [HttpGet("robots.txt")]
    [HttpHead("robots.txt")]
    [AllowAnonymous]
    [OutputCache(PolicyName = OutputCachePolicies.Seo)]
    public IActionResult Robots()
    {
        SetPublicCacheControl();
        return Content(sitemap.GetRobotsTxt(), "text/plain", Encoding.UTF8);
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
