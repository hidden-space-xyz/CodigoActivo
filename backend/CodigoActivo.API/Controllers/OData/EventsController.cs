using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Controllers.OData;

[AllowAnonymous]
public class EventsController(IEventService events) : ODataController
{
    [EnableQuery(PageSize = 100)]
    public IQueryable<EventResponse> Get()
    {
        return events.Query();
    }

    public async Task<ActionResult<EventResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await events.Query().FirstOrDefaultAsync(e => e.Id == key, ct);
        return result is null ? this.ToProblemResult(Error.NotFound(ErrorCode.EventNotFound)) : Ok(result);
    }
}