using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers.Abstractions;

public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId =>
        User.GetUserId()
        ?? throw new InvalidOperationException("No authenticated user on this request.");

    protected ActionResult<T> ToOk<T>(Result<T> result)
    {
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    protected ActionResult ToNoContent(Result result)
    {
        return result.IsSuccess ? NoContent() : ToProblem(result.Error!);
    }

    protected ActionResult ToProblem(Error error)
    {
        if (error.Kind == ErrorKind.NotFound)
        {
            return NotFound();
        }

        var (status, title) = error.Kind switch
        {
            ErrorKind.BadRequest => (StatusCodes.Status400BadRequest, "Validation failed"),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Server error"),
        };
        return Problem(statusCode: status, title: title);
    }
}
