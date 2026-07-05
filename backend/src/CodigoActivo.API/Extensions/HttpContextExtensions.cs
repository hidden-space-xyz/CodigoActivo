using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Extensions;

public static class HttpContextExtensions
{
    public static async Task WriteApiErrorAsync(this HttpContext context, Error error)
    {
        var (statusCode, body) = ApiErrorResponseExtensions.Create(error, context);
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(body);
    }
}
