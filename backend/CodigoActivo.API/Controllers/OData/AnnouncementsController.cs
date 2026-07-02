using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;

namespace CodigoActivo.API.Controllers.OData;

[AllowAnonymous]
public class AnnouncementsController(IAnnouncementService announcements) : ODataController
{
    [EnableQuery(PageSize = 1000)]
    public IQueryable<AnnouncementResponse> Get()
    {
        return announcements.Query();
    }

    public async Task<ActionResult<AnnouncementResponse>> Get(Guid key, CancellationToken ct)
    {
        var result = await announcements.Query().FirstOrDefaultAsync(a => a.Id == key, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
