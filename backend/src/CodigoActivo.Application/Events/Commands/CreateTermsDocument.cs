using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Application.Mapping;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Entities;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Events.Commands;

/// <summary>
/// Carries the input required to create a terms document.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record CreateTermsDocumentCommand(CreateTermsDocumentRequest Request)
    : ICommand<Result<TermsDocumentResponse>>;

/// <summary>
/// Executes the command to create a terms document.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class CreateTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IUnitOfWork uow
) : ICommandHandler<CreateTermsDocumentCommand, Result<TermsDocumentResponse>>
{
    /// <summary>
    /// Handles the request to create a terms document.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains a terms document on success, or an application error on failure.</returns>
    public async Task<Result<TermsDocumentResponse>> HandleAsync(
        CreateTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var name = request.Name.Trim();
        if (await termsDocuments.ExistsAsync(x => x.Name == name, ct))
        {
            return Error.Conflict(ErrorCode.TermsDocumentNameAlreadyExists);
        }

        var termsDocument = new TermsDocument { Name = name, Description = request.Description };
        await termsDocuments.AddAsync(termsDocument, ct);
        await uow.SaveChangesAsync(ct);
        return termsDocument.ToResponse();
    }
}
