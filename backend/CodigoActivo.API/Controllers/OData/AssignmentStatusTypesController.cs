using CodigoActivo.API.Attributes;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

[AllowOnlyAdmin]
public class AssignmentStatusTypesController(IActivityService activities) : ODataController
{
    [EnableQuery(PageSize = 100)]
    public IQueryable<AssignmentStatusTypeResponse> Get()
    {
        return activities.QueryAssignmentStatusTypes();
    }
}
