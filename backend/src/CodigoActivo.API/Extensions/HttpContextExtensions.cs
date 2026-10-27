using CodigoActivo.API.Contracts;
using CodigoActivo.API.Errors;

namespace CodigoActivo.API.Extensions;

/// <summary>
/// Writes error responses outside MVC, such as from the authentication events and middlewares.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Writes the error response of a failure the API decides itself.
    /// </summary>
    /// <param name="context">HTTP context of the request being answered.</param>
    /// <param name="error">Failure with its status and wire code.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task WriteApiErrorAsync(this HttpContext context, ApiError error)
    {
        return context.WriteApiErrorAsync(ApiErrorResponseExtensions.Create(error, context));
    }

    /// <summary>
    /// Writes an error answer already built for this request, with the serializer options every
    /// error response of the API shares.
    /// </summary>
    /// <param name="context">HTTP context of the request being answered.</param>
    /// <param name="answer">Status code and body of the error response.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task WriteApiErrorAsync(
        this HttpContext context,
        (int StatusCode, ApiErrorResponse Body) answer,
        CancellationToken ct = default
    )
    {
        context.Response.StatusCode = answer.StatusCode;
        await context.Response.WriteAsJsonAsync(
            answer.Body,
            ApiErrorResponseExtensions.SerializerOptions,
            ct
        );
    }
}
