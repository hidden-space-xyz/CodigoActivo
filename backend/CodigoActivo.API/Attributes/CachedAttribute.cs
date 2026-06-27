using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CodigoActivo.API.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public sealed class CachedAttribute(string group) : Attribute, IAsyncActionFilter
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

    public int Minutes { get; set; }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        var http = context.HttpContext;
        var cache = http.RequestServices.GetRequiredService<IResponseCacheService>();
        var key = BuildKey(http.Request);
        var ttl = Minutes > 0 ? TimeSpan.FromMinutes(Minutes) : DefaultTtl;

        var ranHere = false;

        var value = await cache.GetOrCreateAsync(
            key,
            group,
            ttl,
            async () =>
            {
                ranHere = true;
                var executed = await next();
                return executed.Result is ObjectResult { Value: not null } result
                    && result.StatusCode is null or StatusCodes.Status200OK
                    ? result.Value
                    : null;
            }
        );

        // When the action did not run in this request (cache hit, or another request
        // filled the entry while we waited), replay the stored payload as the response.
        if (!ranHere && value is not null)
        {
            context.Result = new ObjectResult(value) { StatusCode = StatusCodes.Status200OK };
        }
    }

    private static string BuildKey(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (request.Query.Count == 0)
        {
            return path;
        }

        var query = string.Join(
            '&',
            request
                .Query.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={p.Value}")
        );

        return $"{path}?{query}";
    }
}
