using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Diagnostics;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Logs unexpected failures and returns a safe problem response.
/// </summary>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    /// <summary>
    /// Logs an unhandled exception and writes a safe problem response.
    /// </summary>
    /// <param name="httpContext">The http context value.</param>
    /// <param name="exception">The exception value.</param>
    /// <param name="cancellationToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a valuebool.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        logger.LogError(exception, "Unhandled exception while processing the request");

        var (statusCode, body) = ApiErrorResponseExtensions.CreateInternalError(httpContext);
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);

        return true;
    }
}
