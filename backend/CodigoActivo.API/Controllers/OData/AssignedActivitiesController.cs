using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

[Authorize]
public class AssignedActivitiesController(IActivityService activities) : ODataController
{
    [EnableQuery(PageSize = 100)]
    public IQueryable<AssignedActivityResponse> Get()
    {
        var userId =
            User.GetUserId()
            ?? throw new InvalidOperationException("No authenticated user on this request.");
        return activities.QueryAssigned(userId);
    }
}