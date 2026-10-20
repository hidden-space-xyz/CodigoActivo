using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;

namespace CodigoActivo.Application.TermsDocuments.Commands;

/// <summary>
/// Carries the input required to update the terms document.
/// </summary>
/// <param name="TermsDocumentId">Identifier of the terms document.</param>
/// <param name="Name">Name, unique among terms documents.</param>
/// <param name="Description">Rich-text document holding text only; images are refused.</param>
/// <param name="RequiresSignature">Whether accepting it asks for a handwritten signature.</param>
/// <param name="Kind">Whether it holds conditions or a consent.</param>
public sealed record UpdateTermsDocumentCommand(
    TermsDocumentId TermsDocumentId,
    [property: Required, MaxLength(120), NotBlank] string Name,
    [property: Required, RichText(AllowImages = false), MaxLength(262144)] string Description,
    bool RequiresSignature,
    TermsDocumentKind Kind
) : ICommand<Result>;

/// <summary>
/// Executes the command to update the terms document. The document is locked and reloaded first, so
/// concurrent rewrites run one after another, and its kind stays as it is while an event links it or
/// anyone decided on it, since every rule about those decisions reads the current kind.
/// </summary>
/// <param name="termsDocuments">Repository used to persist and retrieve terms documents.</param>
/// <param name="events">Repository used to persist and retrieve events.</param>
/// <param name="termsAcceptances">Repository used to persist and retrieve terms acceptances.</param>
/// <param name="uow">Unit of work that runs the rewrite in one transaction.</param>
public sealed class UpdateTermsDocumentCommandHandler(
    ITermsDocumentRepository termsDocuments,
    IEventRepository events,
    IEventTermsAcceptanceRepository termsAcceptances,
    IUnitOfWork uow
) : ICommandHandler<UpdateTermsDocumentCommand, Result>
{
    /// <summary>
    /// Handles the request to update the terms document.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result reports success, or an application error on failure.</returns>
    public async Task<Result> HandleAsync(
        UpdateTermsDocumentCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var termsDocument = await termsDocuments.GetByIdAsync(command.TermsDocumentId, ct);
        if (termsDocument is null)
        {
            return Error.NotFound(ApplicationErrorCode.TermsDocumentNotFound);
        }

        var name = command.Name.Trim();
        if (await termsDocuments.NameExistsAsync(name, command.TermsDocumentId, ct))
        {
            return Error.Conflict(ApplicationErrorCode.TermsDocumentNameAlreadyExists);
        }

        return await uow.ExecuteInTransactionAsync(
            attempt => RewriteLockedAsync(termsDocument, name, command, attempt),
            ct
        );
    }

    private async Task<Result> RewriteLockedAsync(
        TermsDocument termsDocument,
        string name,
        UpdateTermsDocumentCommand command,
        CancellationToken ct
    )
    {
        if (!await termsDocuments.LockAsync(termsDocument, ct))
        {
            return Error.NotFound(ApplicationErrorCode.TermsDocumentNotFound);
        }

        if (
            termsDocument.Kind != command.Kind
            && (
                await events.LinksTermsDocumentAsync(termsDocument.Id, ct)
                || await termsAcceptances.AnyForDocumentAsync(termsDocument.Id, ct)
            )
        )
        {
            return Error.Conflict(ApplicationErrorCode.TermsDocumentKindInUse);
        }

        termsDocument.Rewrite(
            name,
            RichText.From(command.Description),
            command.RequiresSignature,
            command.Kind
        );
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
