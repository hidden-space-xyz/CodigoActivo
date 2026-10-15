using CodigoActivo.API.Errors;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Processes HTTP requests to enforce csrf validation. A rejected token is not logged: the client
/// fetches a new token and sends the request again, which is how a token outlived by a sign-in or a
/// sign-out in another tab recovers.
/// </summary>
/// <param name="next">The next value.</param>
/// <param name="antiforgery">The antiforgery value.</param>
public sealed class CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
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
            catch (AntiforgeryValidationException)
            {
                await context.WriteApiErrorAsync(ApiError.Validation(ErrorCode.InvalidCsrfToken));
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
