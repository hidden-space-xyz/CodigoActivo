using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Extensions;

/// <summary>
/// Provides reusable extension methods for http context.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Writes the api error to the response.
    /// </summary>
    /// <param name="context">Database context used for persistence.</param>
    /// <param name="error">Application error associated with a failed result or response.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task WriteApiErrorAsync(this HttpContext context, Error error)
    {
        var (statusCode, body) = ApiErrorResponseExtensions.Create(error, context);
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(body);
    }
}
