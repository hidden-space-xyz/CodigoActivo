using System.Diagnostics;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Processes HTTP requests to enforce request logging.
/// </summary>
/// <param name="next">The next value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class RequestLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestLoggingMiddleware> logger
)
{
    /// <summary>
    /// Processes the current HTTP request and invokes the next middleware.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
            SanitizeForLog(context.Request.Method),
            SanitizeForLog(context.Request.Path.Value),
            context.Response.StatusCode,
            elapsed.TotalMilliseconds
        );
    }

    private static string SanitizeForLog(string? value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace('\r', '_').Replace('\n', '_');
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
