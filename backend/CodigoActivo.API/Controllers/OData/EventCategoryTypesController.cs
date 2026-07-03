using CodigoActivo.API.Attributes;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace CodigoActivo.API.Controllers.OData;

[AllowOnlyAdmin]
public class EventCategoryTypesController(IEventService events) : ODataController
{
    [EnableQuery(PageSize = 100)]
    public IQueryable<EventCategoryTypeResponse> Get()
    {
        return events.QueryCategoryTypes();
    }
}