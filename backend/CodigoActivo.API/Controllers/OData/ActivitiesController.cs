using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Controllers.OData;

[AllowAnonymous]
public class ActivitiesController(IActivityService activities) : ODataController
{
    [EnableQuery(PageSize = 1000)]
    public IQueryable<ActivityResponse> Get()
    {
        return activities.QueryActivities();
    }

    public async Task<ActionResult<ActivityResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await activities.QueryActivities().FirstOrDefaultAsync(a => a.Id == key, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
