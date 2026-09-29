using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.TermsDocuments.Contracts;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.TermsDocuments.Commands;

/// <summary>
/// Carries the input required to create a terms document.
/// </summary>
/// <param name="Request">Validated client request data.</param>
public sealed record CreateTermsDocumentCommand(CreateTermsDocumentRequest Request)
    : ICommand<Result<Guid>>;

/// <summary>
/// Executes the command to create a terms document.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="uow">Unit of work used to commit the changes.</param>
public sealed class CreateTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IUnitOfWork uow
) : ICommandHandler<CreateTermsDocumentCommand, Result<Guid>>
{
    /// <summary>
    /// Handles the request to create a terms document.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the identifier of the created item, or an application error on failure.</returns>
    public async Task<Result<Guid>> HandleAsync(
        CreateTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        var request = command.Request;

        var name = request.Name.Trim();
        if (await termsDocuments.NameExistsAsync(name, ct: ct))
        {
            return Error.Conflict(ErrorCode.TermsDocumentNameAlreadyExists);
        }

        var termsDocument = TermsDocument.Create(name, request.Description);
        await termsDocuments.AddAsync(termsDocument, ct);
        await uow.SaveChangesAsync(ct);
        return termsDocument.Id;
    }
}
