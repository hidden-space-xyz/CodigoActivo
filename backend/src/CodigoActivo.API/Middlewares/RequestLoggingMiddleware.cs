using System.Diagnostics;

namespace CodigoActivo.API.Middlewares;

public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger
)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
            LogRequest(context, Stopwatch.GetElapsedTime(start), null);
        }
        catch (Exception ex)
        {
            LogRequest(context, Stopwatch.GetElapsedTime(start), ex);
            throw;
        }
    }

    private void LogRequest(HttpContext context, TimeSpan elapsed, Exception? exception)
    {
        var level = ResolveLevel(context.Response.StatusCode, exception);
        if (!logger.IsEnabled(level))
        {
            return;
        }

        logger.Log(
            level,
            exception,
            "HTTP {Method} {Path} responded {StatusCode} in {Elapsed:0.0000} ms",
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds
        );
    }

    private static LogLevel ResolveLevel(int statusCode, Exception? exception)
    {
        return statusCode switch
        {
            _ when exception is not null => LogLevel.Error,
            >= StatusCodes.Status500InternalServerError => LogLevel.Error,
            >= StatusCodes.Status400BadRequest => LogLevel.Warning,
            _ => LogLevel.Information,
        };
    }
}
