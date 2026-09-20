using CodigoActivo.API.Diagnostics;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Processes HTTP requests to enforce csrf validation.
/// </summary>
/// <param name="next">The next value.</param>
/// <param name="antiforgery">The antiforgery value.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class CsrfValidationMiddleware(
    RequestDelegate next,
    IAntiforgery antiforgery,
    ILogger<CsrfValidationMiddleware> logger
)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "HEAD",
        "OPTIONS",
        "TRACE",
    };

    /// <summary>
    /// Processes the current HTTP request and invokes the next middleware.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!SafeMethods.Contains(context.Request.Method) && RequiresValidation(context))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException ex)
            {
                logger.CsrfValidationFailed(ex);

                await context.WriteApiErrorAsync(Error.BadRequest(ErrorCode.InvalidCsrfToken));
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>();
        return metadata?.RequiresValidation ?? true;
    }
}
