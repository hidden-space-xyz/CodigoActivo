using CodigoActivo.API.Attributes;
using CodigoActivo.API.Controllers.Abstractions;
using CodigoActivo.API.TermsDocuments.Contracts;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.TermsDocuments.Commands;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Application.TermsDocuments.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;
using Microsoft.AspNetCore.Mvc;

namespace CodigoActivo.API.TermsDocuments;

/// <summary>
/// Exposes HTTP endpoints for managing the terms documents participants accept for events.
/// </summary>
[ApiController]
[Route("api/events/termsDocument")]
[Tags("Events")]
public class TermsDocumentsController : ApiControllerBase
{
    /// <summary>
    /// Executes the terms documents endpoint for events.
    /// </summary>
    /// <param name="query">Query containing the selection criteria.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a paged terms document, or an error response.</returns>
    [HttpGet]
    [AllowOnlyAdmin]
    public async Task<ActionResult<PagedResult<TermsDocumentResponse>>> TermsDocumentsAsync(
        [FromQuery] TermsDocumentListQuery query,
        [FromServices]
            IQueryHandler<ListTermsDocumentsQuery, PagedResult<TermsDocumentResponse>> handler,
        CancellationToken ct
    )
    {
        return Ok(await handler.HandleAsync(new ListTermsDocumentsQuery(query), ct));
    }

    /// <summary>
    /// Creates a terms document.
    /// </summary>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a terms document, or an error response.</returns>
    [HttpPost]
    [AllowOnlyAdmin]
    [ProducesResponseType<TermsDocumentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TermsDocumentResponse>> CreateTermsDocumentAsync(
        [FromBody] CreateTermsDocumentRequest request,
        [FromServices] ICommandHandler<CreateTermsDocumentCommand, Result<TermsDocumentId>> handler,
        [FromServices]
            IQueryHandler<GetTermsDocumentByIdQuery, Result<TermsDocumentResponse>> getById,
        CancellationToken ct
    )
    {
        return await ToCreatedAfterAsync(
            await handler.HandleAsync(request.ToCommand(), ct),
            id => getById.HandleAsync(new GetTermsDocumentByIdQuery(id), ct),
            id => $"/api/events/termsDocument/{id}"
        );
    }

    /// <summary>
    /// Updates a terms document with the supplied data.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="request">Validated client request data.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="getById">Query handler that reads the result of the command.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing a terms document, or an error response.</returns>
    [HttpPut("{termsDocumentId:guid}")]
    [AllowOnlyAdmin]
    public async Task<ActionResult<TermsDocumentResponse>> UpdateTermsDocumentAsync(
        Guid termsDocumentId,
        [FromBody] UpdateTermsDocumentRequest request,
        [FromServices] ICommandHandler<UpdateTermsDocumentCommand, Result> handler,
        [FromServices]
            IQueryHandler<GetTermsDocumentByIdQuery, Result<TermsDocumentResponse>> getById,
        CancellationToken ct
    )
    {
        var documentId = TermsDocumentId.From(termsDocumentId);
        return await ToOkAfterAsync(
            await handler.HandleAsync(request.ToCommand(documentId), ct),
            () => getById.HandleAsync(new GetTermsDocumentByIdQuery(documentId), ct)
        );
    }

    /// <summary>
    /// Deletes a terms document when it is no longer referenced.
    /// </summary>
    /// <param name="termsDocumentId">Identifier of the terms document.</param>
    /// <param name="handler">Application handler that executes the requested use case.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>An HTTP response containing an action, or an error response.</returns>
    [HttpDelete("{termsDocumentId:guid}")]
    [AllowOnlyAdmin]
    public async Task<IActionResult> DeleteTermsDocumentAsync(
        Guid termsDocumentId,
        [FromServices] ICommandHandler<DeleteTermsDocumentCommand, Result> handler,
        CancellationToken ct
    )
    {
        return ToNoContent(
            await handler.HandleAsync(
                new DeleteTermsDocumentCommand(TermsDocumentId.From(termsDocumentId)),
                ct
            )
        );
    }
}
