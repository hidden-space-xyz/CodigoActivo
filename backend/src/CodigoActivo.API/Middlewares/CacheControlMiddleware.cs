using Microsoft.Extensions.Primitives;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Processes HTTP requests to enforce cache control.
/// </summary>
/// <param name="next">The next value.</param>
public class CacheControlMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Processes the current HTTP request and invokes the next middleware.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
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
