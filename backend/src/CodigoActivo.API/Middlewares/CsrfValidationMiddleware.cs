using CodigoActivo.API.Errors;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace CodigoActivo.API.Middlewares;

/// <summary>
/// Validates the antiforgery token of every unsafe request (any method but GET, HEAD, OPTIONS and
/// TRACE) unless its endpoint opts out, answering a missing or wrong token with
/// <see cref="ErrorCode.InvalidCsrfToken"/>. A rejected token is not logged: the client fetches a
/// new token and sends the request again, which is how a token outlived by a sign-in or a sign-out
/// in another tab recovers.
/// </summary>
/// <param name="next">Next middleware of the pipeline.</param>
/// <param name="antiforgery">Antiforgery service that validates the cookie and header token pair.</param>
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
    /// Validates the token when the request needs it and invokes the next middleware only when it
    /// passes.
    /// </summary>
    /// <param name="context">HTTP context of the request.</param>
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
