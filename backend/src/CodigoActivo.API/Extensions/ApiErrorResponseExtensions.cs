using CodigoActivo.API.Contracts;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.API.Extensions;

public static class ApiErrorResponseExtensions
{
    public static (int StatusCode, ApiErrorResponse Body) Create(Error error, HttpContext context)
    {
        var (status, title) = MapKind(error.Kind);
        return (status, new ApiErrorResponse(title, status, error.Code, context.TraceIdentifier));
    }

    public static (int StatusCode, ApiErrorResponse Body) CreateInternalError(HttpContext context)
    {
        const int Status = StatusCodes.Status500InternalServerError;
        return (
            Status,
            new ApiErrorResponse(
                "Internal Server Error",
                Status,
                ErrorCode.UnexpectedError,
                context.TraceIdentifier
            )
        );
    }

    private static (int Status, string Title) MapKind(ErrorKind kind)
    {
        return kind switch
        {
            ErrorKind.BadRequest => (StatusCodes.Status400BadRequest, "Bad Request"),
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
