using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers.Abstractions;

/// <summary>
/// Translates application results into consistent HTTP responses.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// Gets the identifier of the associated user.
    /// </summary>
    protected Guid UserId =>
        User.GetUserId()
        ?? throw new InvalidOperationException("No authenticated user on this request.");

    /// <summary>
    /// Gets whether admin.
    /// </summary>
    protected bool IsAdmin => User.IsAdmin();

    /// <summary>
    /// Converts the value to ok.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="result">The result value.</param>
    /// <returns>An HTTP response containing a t, or an error response.</returns>
    protected ActionResult<T> ToOk<T>(Result<T> result)
    {
        return result.IsSuccess ? Ok(result.Value) : ToProblem(result.Error!);
    }

    /// <summary>
    /// Converts the value to created.
    /// </summary>
    /// <typeparam name="T">Type of item processed by the operation.</typeparam>
    /// <param name="result">The result value.</param>
    /// <param name="location">The location value.</param>
    /// <returns>An HTTP response containing a t, or an error response.</returns>
    protected ActionResult<T> ToCreated<T>(Result<T> result, Func<T, string> location)
    {
        return result.IsFailure
            ? (ActionResult<T>)ToProblem(result.Error!)
            : (ActionResult<T>)Created(
                new Uri(location(result.Value), UriKind.Relative),
                result.Value
            );
    }

    /// <summary>
    /// Converts the value to no content.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    protected ActionResult ToNoContent(Result result)
    {
        return result.IsSuccess ? NoContent() : ToProblem(result.Error!);
    }

    /// <summary>
    /// Converts the value to problem.
    /// </summary>
    /// <param name="error">Application error associated with a failed result or response.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    protected ActionResult ToProblem(Error error)
    {
        var (statusCode, body) = ApiErrorResponseExtensions.Create(error, HttpContext);
        return StatusCode(statusCode, body);
    }
}
