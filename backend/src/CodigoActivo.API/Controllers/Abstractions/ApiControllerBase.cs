using CodigoActivo.API.Errors;
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
    protected Guid CurrentUserId =>
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
    /// Answers a successful operation with its output translated to the HTTP contract, or with the
    /// operation error.
    /// </summary>
    /// <typeparam name="TOutput">Type of the value produced by the operation.</typeparam>
    /// <typeparam name="T">Type of the contract returned to the client.</typeparam>
    /// <param name="result">Outcome of the operation.</param>
    /// <param name="map">Translates the operation output to the HTTP contract.</param>
    /// <returns>An action result with the contract, or the problem describing the failure.</returns>
    protected ActionResult<T> ToOk<TOutput, T>(Result<TOutput> result, Func<TOutput, T> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);
        return result.IsSuccess ? Ok(map(result.Value)) : ToProblem(result.Error!);
    }

    /// <summary>
    /// Answers a successful command with the read model loaded afterwards by a query, or with the
    /// command error.
    /// </summary>
    /// <typeparam name="T">Type of the read model returned to the client.</typeparam>
    /// <param name="result">Outcome of the command.</param>
    /// <param name="read">Query that loads the read model once the command succeeded.</param>
    /// <returns>An action result with the read model, or the problem describing the failure.</returns>
    protected async Task<ActionResult<T>> ToOkAfterAsync<T>(
        Result result,
        Func<Task<Result<T>>> read
    )
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(read);
        return result.IsFailure ? ToProblem(result.Error!) : ToOk(await read());
    }

    /// <summary>
    /// Answers a successful command with the read model loaded afterwards from the command output,
    /// or with the command error.
    /// </summary>
    /// <typeparam name="TOutput">Type of the value produced by the command.</typeparam>
    /// <typeparam name="T">Type of the read model returned to the client.</typeparam>
    /// <param name="result">Outcome of the command.</param>
    /// <param name="read">Query that loads the read model once the command succeeded.</param>
    /// <returns>An action result with the read model, or the problem describing the failure.</returns>
    protected async Task<ActionResult<T>> ToOkAfterAsync<TOutput, T>(
        Result<TOutput> result,
        Func<TOutput, Task<Result<T>>> read
    )
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(read);
        return result.IsFailure ? ToProblem(result.Error!) : ToOk(await read(result.Value));
    }

    /// <summary>
    /// Answers a successful command with the list loaded afterwards from the command output, or
    /// with the command error.
    /// </summary>
    /// <typeparam name="TOutput">Type of the value produced by the command.</typeparam>
    /// <typeparam name="T">Type of the list returned to the client.</typeparam>
    /// <param name="result">Outcome of the command.</param>
    /// <param name="read">Query that loads the list once the command succeeded.</param>
    /// <returns>An action result with the list, or the problem describing the failure.</returns>
    protected async Task<ActionResult<T>> ToOkListAfterAsync<TOutput, T>(
        Result<TOutput> result,
        Func<TOutput, Task<T>> read
    )
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(read);
        return result.IsFailure ? ToProblem(result.Error!) : Ok(await read(result.Value));
    }

    /// <summary>
    /// Answers a successful creation with its location and the read model loaded afterwards, or
    /// with the command error.
    /// </summary>
    /// <typeparam name="TId">Type of the identifier of the new resource.</typeparam>
    /// <typeparam name="T">Type of the read model returned to the client.</typeparam>
    /// <param name="result">Outcome of the command, carrying the identifier of the new resource.</param>
    /// <param name="read">Query that loads the read model of the new resource.</param>
    /// <param name="location">Builds the relative location of the new resource.</param>
    /// <returns>A created result with the read model, or the problem describing the failure.</returns>
    protected async Task<ActionResult<T>> ToCreatedAfterAsync<TId, T>(
        Result<TId> result,
        Func<TId, Task<Result<T>>> read,
        Func<Guid, string> location
    )
        where TId : struct, IEntityId<TId>
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(location);
        if (result.IsFailure)
        {
            return ToProblem(result.Error!);
        }

        var created = await read(result.Value);
        return created.IsFailure
            ? ToProblem(created.Error!)
            : Created(new Uri(location(result.Value.Value), UriKind.Relative), created.Value);
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
        return ToProblem(ApiError.From(error));
    }

    /// <summary>
    /// Converts a failure the API decides itself to an error response.
    /// </summary>
    /// <param name="error">Failure with its wire code.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    protected ActionResult ToProblem(ApiError error)
    {
        var (statusCode, body) = ApiErrorResponseExtensions.Create(error, HttpContext);
        return StatusCode(statusCode, body);
    }
}
