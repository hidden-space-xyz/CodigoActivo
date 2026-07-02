using CodigoActivo.API.Attributes;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

[AllowOnlyAdmin]
public class ActivityModalityTypesController(IActivityService activities) : ODataController
{
    [EnableQuery(PageSize = 1000)]
    public IQueryable<ActivityModalityTypeResponse> Get()
    {
        return activities.QueryModalityTypes();
    }
}
