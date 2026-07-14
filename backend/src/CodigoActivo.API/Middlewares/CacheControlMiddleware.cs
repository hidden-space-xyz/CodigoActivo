using Microsoft.Extensions.Primitives;

namespace CodigoActivo.API.Middlewares;

public class CacheControlMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.OnStarting(
                static state =>
                {
                    var response = ((HttpContext)state).Response;
                    if (StringValues.IsNullOrEmpty(response.Headers.CacheControl))
                        response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                },
                context
            );
        }

        return next(context);
    }
}
