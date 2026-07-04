using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Writes an <see cref="ApiErrorResponse"/> for non-MVC pipelines (middleware, auth events)
    /// so every error body goes through the same <see cref="ApiErrorResponseExtensions"/> shaping.
    /// </summary>
    public static async Task WriteApiErrorAsync(this HttpContext context, Error error)
    {
        var (statusCode, body) = ApiErrorResponseExtensions.Create(error, context);
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(body);
    }
}
