using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.Errors;
using CodigoActivo.API.Security;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Accounts.Commands;
using CodigoActivo.Application.Accounts.Contracts;
using CodigoActivo.Application.Common.Catalogs;
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
/// Exposes HTTP endpoints for querying and managing user accounts and the minors of their
/// households.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    /// <summary>
    /// Lists the users that match the supplied filters: every account for an administrator, and
    /// only their own account and their dependents for anyone else.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a page of users.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> ListAsync(
        [FromQuery] UserListQuery query,
        [FromServices] IQueryHandler<ListUsersQuery, PagedResult<UserResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUsersQuery(query), ct));
    }

    /// <summary>
    /// Lists the catalog of membership types.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the membership types.</returns>
    [HttpGet("types")]
    [AllowOnlyStaff(StaffRole.Administrator)]
    public async Task<ActionResult<IReadOnlyList<UserTypeResponse>>> TypesAsync(
        [FromServices] IQueryHandler<ListUserTypesQuery, IReadOnlyList<UserTypeResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUserTypesQuery(), ct));
    }

    /// <summary>
    /// Lists the catalog of account statuses.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the account statuses.</returns>
    [HttpGet("status-types")]
    [AllowOnlyStaff(StaffRole.Administrator)]
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
    [AllowOnlyStaff(StaffRole.Administrator)]
    public async Task<ActionResult<UserResponse>> GetAsync(
        Guid userId,
        [FromServices] IQueryHandler<GetUserByIdQuery, Result<UserResponse>> handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new GetUserByIdQuery(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Updates the selected user with the validated request. Only the user or their guardian may,
    /// never an administrator. Replacing the email or the phones requires the caller's password, so
    /// the endpoint shares the credential rate limits; replacing the email also requires a current
    /// code of the caller's second factor, and the new email only applies once its holder confirms
    /// it through the emailed link.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the updated user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the user, or an error response.</returns>
    [HttpPut("{userId:guid}")]
    [AllowOnlySelf(AdministratorsAllowed = false)]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [PasswordHashing]
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
    /// administrator, which is itself refused. Only the initial administrator deletes other
    /// accounts, and a guardian their minors. The caller's own account is refused here too:
    /// erasing it requires the password and the second factor through <c>POST /api/me/deletion</c>.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
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
    /// Changes the staff role of a user; only the initial administrator does. Giving a role
    /// requires the caller's password, so the endpoint shares the credential rate limits. The
    /// caller's own role is refused, neither a dependent nor an account with an unconfirmed email
    /// gets one, and the initial administrator always stays an administrator.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpPatch("{userId:guid}/staff-role")]
    [AllowOnlyStaff(StaffRole.Administrator)]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [PasswordHashing]
    public async Task<IActionResult> SetStaffRoleAsync(
        Guid userId,
        [FromBody] SetStaffRoleRequest request,
        [FromServices] ICommandHandler<SetStaffRoleCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Designates the account for data protection, the only one that consults the blocked data;
    /// only the initial administrator does, with its password, so the endpoint shares the
    /// credential rate limits. The account designated until now stops being designated.
    /// </summary>
    /// <param name="userId">Identifier of the account designated from now on.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpPost("{userId:guid}/privacy-officer")]
    [AllowOnlyStaff(StaffRole.Administrator)]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [PasswordHashing]
    public async Task<IActionResult> DesignatePrivacyOfficerAsync(
        Guid userId,
        [FromBody] DesignatePrivacyOfficerRequest request,
        [FromServices] ICommandHandler<DesignatePrivacyOfficerCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(request.ToCommand(UserId.From(userId)), ct));
    }

    /// <summary>
    /// Returns a user's second factor to email so they can log in again after losing their
    /// authenticator; it also drops a pending login code and lifts the second-factor lock. The
    /// acting administrator confirms their own password, and is refused on their own account,
    /// whose authenticator they remove themselves with its current code.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpPost("{userId:guid}/two-factor/reset")]
    [AllowOnlyStaff(StaffRole.Administrator)]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [PasswordHashing]
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
    /// Adds a minor to the household of the selected user, who must be the caller.
    /// </summary>
    /// <param name="userId">Identifier of the guardian.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the created minor.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 201 response with the minor and its location, or an error response.</returns>
    [HttpPost("{userId:guid}/children")]
    [AllowOnlySelf(AdministratorsAllowed = false)]
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
    /// Changes the password of the caller's own account, which needs its current password. Every
    /// session of the account ends and the holder is emailed a security alert.
    /// </summary>
    /// <param name="userId">Identifier of the user; it must be the caller.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A 204 response, or an error response.</returns>
    [HttpPatch("{userId:guid}/password")]
    [AllowOnlySelf]
    [EnableRateLimiting(SecurityPolicies.Credentials)]
    [PasswordHashing]
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
    /// Changes the membership type of the user. The caller's own type is refused.
    /// </summary>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="userTypeId">
    /// Identifier of the new membership type; one that names no member of the catalog answers
    /// <see cref="ErrorCode.RequestValidationFailed"/>.
    /// </param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the updated user.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the user, or an error response.</returns>
    [HttpPatch("{userId:guid}/change-type")]
    [AllowOnlyStaff(StaffRole.Administrator)]
    public async Task<ActionResult<UserResponse>> ChangeTypeAsync(
        Guid userId,
        [FromQuery] Guid userTypeId,
        [FromServices] ICommandHandler<ChangeUserTypeCommand, Result> handler,
        [FromServices] IQueryHandler<GetUserByIdQuery, Result<UserResponse>> getById,
        CancellationToken ct
    )
    {
        if (!CatalogIds.UserTypes.TryGetValue(userTypeId, out var userType))
        {
            return ToProblem(ApiError.Validation(ErrorCode.RequestValidationFailed));
        }

        return await ToOkAfterAsync(
            await handler.HandleAsync(new ChangeUserTypeCommand(UserId.From(userId), userType), ct),
            () => getById.HandleAsync(new GetUserByIdQuery(UserId.From(userId)), ct)
        );
    }
}
