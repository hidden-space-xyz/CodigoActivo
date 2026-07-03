using CodigoActivo.API.Extensions;
using Microsoft.AspNetCore.Diagnostics;

namespace CodigoActivo.API.Middlewares;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
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
