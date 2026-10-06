using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Partners;

namespace CodigoActivo.Application.Partners.Commands;

/// <summary>
/// Carries the input required to delete the partner.
/// </summary>
/// <param name="PartnerId">Identifier of the partner.</param>
public sealed record DeletePartnerCommand(PartnerId PartnerId) : ICommand<Result>;

/// <summary>
/// Executes the command to delete the partner.
/// </summary>
/// <param name="partners">Repository used to persist and retrieve partners.</param>
public sealed class DeletePartnerCommandHandler(IPartnerRepository partners)
    : ICommandHandler<DeletePartnerCommand, Result>
{
    /// <summary>
    /// Handles the request to delete the partner.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result indicates success or contains the application error.</returns>
    public async Task<Result> HandleAsync(
        DeletePartnerCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var partner = await partners.GetByIdAsync(command.PartnerId, ct);
        if (partner is null)
        {
            return Error.NotFound(ApplicationErrorCode.PartnerNotFound);
        }

        partner.Delete();
        partners.Remove(partner);
        return Result.Success();
    }
}
