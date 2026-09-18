using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Security;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Controllers;

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
        [FromServices] ListUsersQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUsersQuery(query, UserId, IsAdmin), ct));
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
        [FromServices] ListUserTypesQueryHandler handler,
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
        [FromServices] ListUserStatusTypesQueryHandler handler,
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
        [FromServices] GetUserByIdQueryHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetUserByIdQuery(userId), ct));
    }

    /// <summary>
    /// Updates the selected user with the validated request. Replacing the login identifiers
    /// requires the caller's password, so the endpoint shares the credential rate limits.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPut("{userId:guid}")]
    [AllowOnlySelf]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    public async Task<ActionResult<UserResponse>> UpdateAsync(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        [FromServices] UpdateUserCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new UpdateUserCommand(userId, UserId, request), ct));
    }

    /// <summary>
    /// Deletes the selected user. The caller's own account is refused here: erasing it requires
    /// the password and the second factor through <c>POST /api/me/deletion</c>.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{userId:guid}")]
    [AllowOnlySelf]
    public async Task<IActionResult> DeleteAsync(
        Guid userId,
        [FromServices] DeleteUserCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteUserCommand(userId, UserId), ct));
    }

    /// <summary>
    /// Sets the admin state. Granting it requires the caller's password, so the endpoint shares
    /// the credential rate limits.
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
        [FromServices] SetAdminCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new SetAdminCommand(userId, UserId, request), ct)
        );
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
        [FromServices] ResetTwoFactorCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new ResetTwoFactorCommand(userId, UserId, request), ct)
        );
    }

    /// <summary>
    /// Adds a child to the current unit of work.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPost("{userId:guid}/children")]
    [AllowOnlySelf]
    public async Task<ActionResult<UserResponse>> AddChildAsync(
        Guid userId,
        [FromBody] RegisterMinorRequest request,
        [FromServices] AddChildCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new AddChildCommand(userId, request), ct));
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
        [FromServices] ChangePasswordCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new ChangePasswordCommand(userId, request), ct)
        );
    }

    /// <summary>
    /// Changes the type to the requested value.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="userTypeId">Identifier of the user type.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a user, or an error response.</returns>
    [HttpPatch("{userId:guid}/change-type")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<UserResponse>> ChangeTypeAsync(
        Guid userId,
        [FromQuery] Guid userTypeId,
        [FromServices] ChangeUserTypeCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new ChangeUserTypeCommand(userId, userTypeId), ct));
    }
}
