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
public class UsersController(IUserService users) : ApiControllerBase
{
    [HttpGet]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken ct)
    {
        return Ok(await users.GetAllAsync(ct));
    }

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
    [InvalidatesCache(nameof(DashboardSummaryResponse))]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken ct)
    {
        return ToNoContent(await users.DeleteAsync(userId, ct));
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

    [HttpGet("statusTypes")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<UserStatusTypeResponse>>> GetStatusTypes(
        CancellationToken ct
    )
    {
        return Ok(await users.GetUserStatusTypesAsync(ct));
    }

    [HttpGet("types")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<IReadOnlyList<UserTypeResponse>>> GetTypes(CancellationToken ct)
    {
        return Ok(await users.GetUserTypesAsync(ct));
    }
}
