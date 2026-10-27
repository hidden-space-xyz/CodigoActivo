using CodigoActivo.API.Contracts;
using CodigoActivo.API.Diagnostics;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Routing.Patterns;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Logs unexpected failures and returns a safe problem response. A commit refused because another
/// transaction changed the same data first is an expected outcome, answered without a log entry as
/// a conflict the client can send again, <see cref="ErrorCode.ConcurrentUpdate"/>. The entry of an
/// unexpected failure names the endpoint by its route pattern, never by the requested path, which
/// carries the identifiers of the users it is about.
/// </summary>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private const string UnmatchedRoute = "(no endpoint)";

    /// <summary>
    /// Answers an exception the request did not handle: a concurrent update as a 409 conflict, and
    /// anything else, after logging it, as a 500 error that tells nothing about its cause.
    /// </summary>
    /// <param name="httpContext">HTTP context of the failed request.</param>
    /// <param name="exception">Exception that ended the request.</param>
    /// <param name="cancellationToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is always <see langword="true"/>, since every exception is answered.
    /// </returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        await httpContext.WriteApiErrorAsync(Answer(httpContext, exception), cancellationToken);

        return true;
    }

    private (int StatusCode, ApiErrorResponse Body) Answer(
        HttpContext httpContext,
        Exception exception
    )
    {
        if (exception is ConcurrentUpdateException)
        {
            return ApiErrorResponseExtensions.Create(
                ApiError.Conflict(ErrorCode.ConcurrentUpdate),
                httpContext
            );
        }

        logger.UnhandledRequestException(
            httpContext.Request.Method,
            RouteTemplate(httpContext),
            exception
        );
        return ApiErrorResponseExtensions.CreateInternalError(httpContext);
    }

    /// <summary>
    /// Reads the route pattern of the endpoint the request had reached. The exception handler clears
    /// the endpoint before calling this handler, so the one captured by the exception feature is
    /// preferred.
    /// </summary>
    /// <param name="httpContext">Context of the failed request.</param>
    /// <returns>The route pattern, or a fixed literal when no endpoint matched.</returns>
    private static string RouteTemplate(HttpContext httpContext)
    {
        var endpoint =
            httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint
            ?? httpContext.GetEndpoint();

        return endpoint is RouteEndpoint route ? RawText(route.RoutePattern) : UnmatchedRoute;
    }

    private static string RawText(RoutePattern pattern)
    {
        return string.IsNullOrEmpty(pattern.RawText) ? UnmatchedRoute : pattern.RawText;
    }
}
