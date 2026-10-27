using System.Text.Json;
using System.Text.Json.Serialization;
using CodigoActivo.API.Contracts;
using CodigoActivo.API.Errors;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Http.Features;

namespace CodigoActivo.API.Extensions;

/// <summary>
/// Builds the status code and body of every error response of the API.
/// </summary>
public static class ApiErrorResponseExtensions
{
    /// <summary>
    /// Gets the serializer options for error bodies written outside MVC: web defaults with every
    /// error code written as its name, exactly like the controller responses.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions { get; } =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false),
            },
        };

    /// <summary>
    /// Creates the error response of a failure, with the status its kind maps to.
    /// </summary>
    /// <param name="error">Failure with its kind and wire code.</param>
    /// <param name="context">HTTP context of the failed request.</param>
    /// <returns>The status code and body of the error response.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The kind of the failure maps to no status.</exception>
    public static (int StatusCode, ApiErrorResponse Body) Create(
        ApiError error,
        HttpContext context
    )
    {
        var (status, title) = MapKind(error.Kind);
        return (status, new ApiErrorResponse(title, status, error.Code, TraceIdOf(context)));
    }

    /// <summary>
    /// Creates the 500 error response of an unexpected failure, which tells nothing about its cause.
    /// </summary>
    /// <param name="context">HTTP context of the failed request.</param>
    /// <returns>The status code and body of the error response.</returns>
    public static (int StatusCode, ApiErrorResponse Body) CreateInternalError(HttpContext context)
    {
        const int Status = StatusCodes.Status500InternalServerError;
        return (
            Status,
            new ApiErrorResponse(
                "Internal Server Error",
                Status,
                ErrorCode.UnexpectedError,
                TraceIdOf(context)
            )
        );
    }

    /// <summary>
    /// Creates the 429 error response of a request refused by a rate or concurrency limit.
    /// </summary>
    /// <param name="context">HTTP context of the refused request.</param>
    /// <returns>The status code and body of the error response.</returns>
    public static (int StatusCode, ApiErrorResponse Body) CreateTooManyRequests(HttpContext context)
    {
        const int Status = StatusCodes.Status429TooManyRequests;
        return (
            Status,
            new ApiErrorResponse(
                "Too Many Requests",
                Status,
                ErrorCode.TooManyRequests,
                TraceIdOf(context)
            )
        );
    }

    /// <summary>
    /// Gets the reference an error response shows: the trace id of the request activity, the one
    /// the diagnostic and audit logs store, or the connection-based identifier when there is none.
    /// </summary>
    /// <param name="context">HTTP context of the request.</param>
    /// <returns>The reference of the request.</returns>
    private static string TraceIdOf(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return
            context.Features.Get<IHttpActivityFeature>()?.Activity is { } activity
            && activity.TraceId != default
            ? activity.TraceId.ToHexString()
            : context.TraceIdentifier;
    }

    private static (int Status, string Title) MapKind(ErrorKind kind)
    {
        return kind switch
        {
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Bad Request"),
            ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported error kind."
            ),
        };
    }
}
