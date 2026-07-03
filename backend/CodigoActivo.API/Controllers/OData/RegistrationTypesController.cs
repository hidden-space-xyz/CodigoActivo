using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

[AllowAnonymous]
public class RegistrationTypesController(IUserService users) : ODataController
{
    [EnableQuery(PageSize = 100)]
    public IQueryable<RegistrationTypeResponse> Get()
    {
        return users.QueryRegistrationTypes();
    }
}