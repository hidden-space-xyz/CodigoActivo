using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UserCommandsController(IUserService users) : ApiControllerBase
{
    [HttpGet("{userId:guid}")]
    [AllowOnlySelf]
    public async Task<ActionResult<UserResponse>> GetById(Guid userId, CancellationToken ct)
    {
        return ToOk(await users.GetByIdAsync(userId, ct));
    }

    [HttpPut("{userId:guid}")]
    [AllowOnlySelf]
    public async Task<ActionResult<UserResponse>> Update(
        Guid userId,
        [FromBody] UpdateUserRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await users.UpdateAsync(userId, request, ct));
    }

    [HttpDelete("{userId:guid}")]
    [AllowOnlySelf]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken ct)
    {
        return ToNoContent(await users.DeleteAsync(userId, ct));
    }

    [HttpGet("{userId:guid}/children")]
    [AllowOnlySelf]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetChildren(
        Guid userId,
        CancellationToken ct
    )
    {
        return Ok(await users.GetChildrenAsync(userId, ct));
    }

    [HttpPost("{userId:guid}/children")]
    [AllowOnlySelf]
    public async Task<ActionResult<UserResponse>> AddChild(
        Guid userId,
        [FromBody] RegisterMinorRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await users.AddChildAsync(userId, request, ct));
    }

    [HttpPatch("{userId:guid}/role")]
    [AllowOnlySelf]
    public async Task<ActionResult<UserResponse>> SetRole(
        Guid userId,
        [FromBody] SetUserRoleRequest request,
        CancellationToken ct
    )
    {
        return ToOk(await users.SetRoleAsync(userId, request.RoleId, ct));
    }

    [HttpPatch("{userId:guid}/password")]
    [AllowOnlySelf]
    public async Task<IActionResult> ChangePassword(
        Guid userId,
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct
    )
    {
        return ToNoContent(await users.ChangePasswordAsync(userId, request, ct));
    }

    [HttpPatch("{userId:guid}/change-type")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<UserResponse>> ChangeType(
        Guid userId,
        [FromQuery] Guid roleId,
        CancellationToken ct
    )
    {
        return ToOk(await users.ChangeTypeAsync(userId, roleId, ct));
    }
}
