using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Antiforgery;

namespace CodigoActivo.API.Middlewares;

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

    public async Task InvokeAsync(HttpContext context)
    {
        if (!SafeMethods.Contains(context.Request.Method) && RequiresValidation(context))
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException ex)
            {
                logger.LogWarning(ex, "CSRF validation failed");

                var (statusCode, body) = ApiErrorResponseExtensions.Create(
                    Error.BadRequest(ErrorCode.InvalidCsrfToken),
                    context
                );
                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(body);
                return;
            }

        await next(context);
    }

    private static bool RequiresValidation(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<IAntiforgeryMetadata>();
        return metadata?.RequiresValidation ?? true;
    }
}