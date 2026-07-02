using CodigoActivo.API.Attributes;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

[AllowOnlyAdmin]
public class UsersController(IUserService users) : ODataController
{
    [EnableQuery(PageSize = 1000)]
    public IQueryable<UserResponse> Get()
    {
        return users.QueryUsers();
    }
}
