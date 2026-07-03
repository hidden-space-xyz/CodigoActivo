using CodigoActivo.API.Attributes;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Controllers.OData;

[Authorize]
public class UsersController(IUserService users) : ODataController
{
    [EnableQuery(PageSize = 100)]
    public IQueryable<UserResponse> Get()
    {
        if (User.IsAdmin())
        {
            return users.QueryUsers();
        }

        var callerId =
            User.GetUserId()
            ?? throw new InvalidOperationException("No authenticated user on this request.");
        return users.QueryUsers().Where(user => user.Id == callerId || user.ParentId == callerId);
    }

    [AllowOnlyAdmin]
    public async Task<ActionResult<UserResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await users.QueryUsers().FirstOrDefaultAsync(user => user.Id == key, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
