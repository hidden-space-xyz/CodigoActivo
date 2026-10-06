using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Contracts;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Users;

/// <summary>
/// Exposes HTTP endpoints for querying and managing users.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    /// <summary>
    /// Lists the users that match the supplied filters.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged user, or an error response.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> ListAsync(
        [FromQuery] UserListQuery query,
        [FromServices] IQueryHandler<ListUsersQuery, PagedResult<UserResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUsersQuery(query, CurrentUserId, IsAdmin), ct));
    }

    /// <summary>
    /// Executes the types endpoint for users.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user type, or an error response.</returns>
    [HttpGet("types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<UserTypeResponse>>> TypesAsync(
        [FromServices] IQueryHandler<ListUserTypesQuery, IReadOnlyList<UserTypeResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUserTypesQuery(), ct));
    }

    /// <summary>
    /// Executes the status types endpoint for users.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user status type, or an error response.</returns>
    [HttpGet("status-types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<UserStatusTypeResponse>>> StatusTypesAsync(
        [FromServices]
            IQueryHandler<ListUserStatusTypesQuery, IReadOnlyList<UserStatusTypeResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUserStatusTypesQuery(), ct));
    }

    /// <summary>
    /// Gets the requested user.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpGet("{userId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<UserResponse>> GetAsync(
        Guid userId,
        [FromServices] IQueryHandler<GetUserByIdQuery, Result<UserResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetUserByIdQuery(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Updates the selected user with the validated request. Replacing the email or the phone
    /// requires the caller's password, so the endpoint shares the credential rate limits.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPut("{userId:guid}")]
    [AllowOnlySelf]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<UserResponse>> UpdateAsync(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        [FromServices] ICommandHandler<UpdateUserCommand, Result> handler,
        [FromServices] IQueryHandler<GetUserByIdQuery, Result<UserResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(UserId.From(userId)), ct),
            () => getById.HandleAsync(new GetUserByIdQuery(UserId.From(userId)), ct)
        );
    }

    /// <summary>
    /// Deletes the selected user, handing the content credited to the account over to the initial
    /// administrator, which is itself refused. The caller's own account is refused here too:
    /// erasing it requires the password and the second factor through <c>POST /api/me/deletion</c>.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{userId:guid}")]
    [AllowOnlySelf]
    public async Task<IActionResult> DeleteAsync(
        Guid userId,
        [FromServices] ICommandHandler<DeleteUserCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new DeleteUserCommand(UserId.From(userId)), ct)
        );
    }

    /// <summary>
    /// Sets the admin state. Granting it requires the caller's password, so the endpoint shares
    /// the credential rate limits. The initial administrator always keeps it.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPatch("{userId:guid}/admin")]
    [AllowOnlyAdmin]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<IActionResult> SetAdminAsync(
        Guid userId,
        [FromBody] SetAdminRequest request,
        [FromServices] ICommandHandler<SetAdminCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Returns a user's second factor to email so they can log in again after losing their
    /// authenticator. The acting administrator confirms their own password.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPost("{userId:guid}/two-factor/reset")]
    [AllowOnlyAdmin]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<IActionResult> ResetTwoFactorAsync(
        Guid userId,
        [FromBody] ResetTwoFactorRequest request,
        [FromServices] ICommandHandler<ResetTwoFactorCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Adds a child to the current unit of work.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPost("{userId:guid}/children")]
    [AllowOnlySelf]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<UserResponse>> AddChildAsync(
        Guid userId,
        [FromBody] RegisterMinorRequest request,
        [FromServices] ICommandHandler<AddChildCommand, Result<UserId>> handler,
        [FromServices] IQueryHandler<GetUserByIdQuery, Result<UserResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(
                new AddChildCommand(UserId.From(userId), request.ToDraft()),
                ct
            ),
            id => getById.HandleAsync(new GetUserByIdQuery(id), ct),
            id => $"/api/users/{id}"
        );
    }

    /// <summary>
    /// Changes the password to the requested value.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpPatch("{userId:guid}/password")]
    [AllowOnlySelf]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<IActionResult> ChangePasswordAsync(
        Guid userId,
        [FromBody] ChangePasswordRequest request,
        [FromServices] ICommandHandler<ChangePasswordCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Changes the type to the requested value.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="userTypeId">Identifier of the user type.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPatch("{userId:guid}/change-type")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<UserResponse>> ChangeTypeAsync(
        Guid userId,
        [FromQuery] Guid userTypeId,
        [FromServices] ICommandHandler<ChangeUserTypeCommand, Result> handler,
        [FromServices] IQueryHandler<GetUserByIdQuery, Result<UserResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToOkAfterAsync(
            await handler.HandleAsync(
                new ChangeUserTypeCommand(UserId.From(userId), userTypeId),
                ct
            ),
            () => getById.HandleAsync(new GetUserByIdQuery(UserId.From(userId)), ct)
        );
    }
}
