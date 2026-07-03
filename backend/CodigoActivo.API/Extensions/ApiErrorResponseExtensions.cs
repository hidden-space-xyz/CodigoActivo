using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Extensions;

public sealed record ApiErrorResponse(string Title, int Status, ErrorCode Code, string TraceId);

public static class ApiErrorResponseExtensions
{
    public static ActionResult<T> ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        return result.IsSuccess ? controller.Ok(result.Value) : controller.ToProblemResult(result.Error!);
    }

    public static ActionResult ToActionResult(this ControllerBase controller, Result result)
    {
        return result.IsSuccess ? controller.NoContent() : controller.ToProblemResult(result.Error!);
    }

    public static ActionResult ToProblemResult(this ControllerBase controller, Error error)
    {
        var (statusCode, body) = Create(error, controller.HttpContext);
        return controller.StatusCode(statusCode, body);
    }

    public static (int StatusCode, ApiErrorResponse Body) Create(Error error, HttpContext context)
    {
        var (status, title) = MapKind(error.Kind);
        return (status, new ApiErrorResponse(title, status, error.Code, context.GetOrSetTraceId()));
    }

    public static (int StatusCode, ApiErrorResponse Body) CreateInternalError(HttpContext context)
    {
        const int Status = StatusCodes.Status500InternalServerError;
        return (
            Status,
            new ApiErrorResponse("Internal Server Error", Status, ErrorCode.UnexpectedError, context.GetOrSetTraceId())
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
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported error kind.")
        };
    }
}