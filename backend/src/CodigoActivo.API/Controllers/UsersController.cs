using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Querying;
using CodigoActivo.Application.Users.Commands;
using CodigoActivo.Application.Users.Queries;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponse>>> ListAsync(
        [FromQuery] UserListQuery query,
        [FromServices] ListUsersQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUsersQuery(query, UserId, IsAdmin), ct));
    }

    [HttpGet("types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<UserTypeResponse>>> TypesAsync(
        [FromServices] ListUserTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUserTypesQuery(), ct));
    }

    [HttpGet("status-types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<UserStatusTypeResponse>>> StatusTypesAsync(
        [FromServices] ListUserStatusTypesQueryHandler handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListUserStatusTypesQuery(), ct));
    }

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

    [HttpPut("{userId:guid}")]
    [AllowOnlySelf]
    public async Task<ActionResult<UserResponse>> UpdateAsync(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        [FromServices] UpdateUserCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToOk(await handler.HandleAsync(new UpdateUserCommand(userId, request), ct));
    }

    [HttpDelete("{userId:guid}")]
    [AllowOnlySelf]
    public async Task<IActionResult> DeleteAsync(
        Guid userId,
        [FromServices] DeleteUserCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(await handler.HandleAsync(new DeleteUserCommand(userId), ct));
    }

    [HttpPatch("{userId:guid}/admin")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> SetAdminAsync(
        Guid userId,
        [FromBody] SetAdminRequest request,
        [FromServices] SetAdminCommandHandler handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(new SetAdminCommand(userId, request.IsAdmin), ct)
        );
    }

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

    [HttpPatch("{userId:guid}/password")]
    [AllowOnlySelf]
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
