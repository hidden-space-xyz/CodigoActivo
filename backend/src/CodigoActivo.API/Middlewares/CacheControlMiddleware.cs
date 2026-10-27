using Microsoft.Extensions.Primitives;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Marks every <c>/api</c> response <c>Cache-Control: no-store</c> unless the endpoint set a
/// <c>Cache-Control</c> of its own, so no browser or proxy keeps an API answer it was not meant to.
/// </summary>
/// <param name="next">Next middleware of the pipeline.</param>
public class CacheControlMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Registers the default header for an <c>/api</c> request and invokes the next middleware.
    /// </summary>
    /// <param name="context">HTTP context of the request.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.OnStarting(
                static state =>
                {
                    var response = ((HttpContext)state).Response;
                    if (StringValues.IsNullOrEmpty(response.Headers.CacheControl))
                    {
                        response.Headers.CacheControl = "no-store";
                    }

                    return Task.CompletedTask;
                },
                context
            );
        }

        return next(context);
    }
}
