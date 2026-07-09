using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers.Abstractions;

public abstract class ApiControllerBase : ControllerBase
{
    protected Guid UserId =>
        User.GetUserId()
        ?? throw new InvalidOperationException("No authenticated user on this request.");

    protected bool IsAdmin => User.IsAdmin();

    protected ActionResult<T> ToOk<T>(Result<T> result)
    {
        return this.ToActionResult(result);
    }

    protected ActionResult<T> ToCreated<T>(Result<T> result, Func<T, string> location)
    {
        return result.IsFailure
            ? (ActionResult<T>)ToProblem(result.Error!)
            : (ActionResult<T>)Created(location(result.Value), result.Value);
    }

    protected ActionResult ToNoContent(Result result)
    {
        return this.ToActionResult(result);
    }

    protected ActionResult ToProblem(Error error)
    {
        return this.ToProblemResult(error);
    }
}
