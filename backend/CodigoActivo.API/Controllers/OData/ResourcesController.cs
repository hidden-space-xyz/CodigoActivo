using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Controllers.OData;

[AllowAnonymous]
public class ResourcesController(IResourceService resources) : ODataController
{
    [EnableQuery(PageSize = 1000)]
    public IQueryable<ResourceResponse> Get()
    {
        return resources.Query();
    }

    public async Task<ActionResult<ResourceResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await resources.Query().FirstOrDefaultAsync(r => r.Id == key, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
