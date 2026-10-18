using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Legal.Contracts;
using CodigoActivo.Application.Legal.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CodigoActivo.API.Legal;

/// <summary>
/// Exposes the legal identity of the association that the privacy policy shows.
/// </summary>
[ApiController]
[Route("api/legal-entity")]
public class LegalController : ApiControllerBase
{
    /// <summary>
    /// Returns the legal identity configured for the deployment.
    /// </summary>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing the legal identity.</returns>
    [HttpGet]
    [AllowAnonymous]
    [OutputCache(NoStore = true)]
    public async Task<ActionResult<LegalEntityResponse>> GetAsync(
        [FromServices] IQueryHandler<GetLegalEntityQuery, LegalEntityResponse> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new GetLegalEntityQuery(), ct));
    }
}
