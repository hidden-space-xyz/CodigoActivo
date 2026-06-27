using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Middlewares;

public sealed class CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "HEAD",
        "OPTIONS",
        "TRACE",
    };

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        if (!SafeMethods.Contains(context.Request.Method) && RequiresValidation(context))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException ex)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await problemDetails.TryWriteAsync(
                    new ProblemDetailsContext
                    {
                        HttpContext = context,
                        ProblemDetails = new ProblemDetails
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Title = "Validation failed",
                            Detail = $"Invalid or missing CSRF token. {ex.Message}",
                        },
                    }
                );
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
