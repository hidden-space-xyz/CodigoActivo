using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Files;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;

namespace CodigoActivo.Application.Partners.Commands;

/// <summary>
/// Carries the input required to create a partner.
/// </summary>
/// <param name="Name">Human-readable name, up to 200 characters.</param>
/// <param name="FromDate">Day the collaboration started; today at the latest.</param>
/// <param name="Tier">Sponsorship tier, zero or more.</param>
/// <param name="Website">
/// HTTP or HTTPS address of up to 500 characters, or <see langword="null"/>.
/// </param>
/// <param name="ThumbnailId">Identifier of the logo file, which must exist.</param>
public sealed record CreatePartnerCommand(
    [property: Required, MaxLength(200)] string Name,
    [property: NotDefaultOrFutureDate] DateOnly FromDate,
    [property: Range(0, int.MaxValue)] int Tier,
    [property: HttpUrl, MaxLength(500)] string? Website,
    StoredFileId ThumbnailId
) : ICommand<Result<PartnerId>>, IRoutineCommand;

/// <summary>
/// Executes the command to create a partner.
/// </summary>
/// <param name="partners">Repository used to persist and retrieve partners.</param>
/// <param name="files">Repository used to persist and retrieve files.</param>
/// <param name="currentUser">Person the use case runs for, recorded as the creator.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class CreatePartnerCommandHandler(
    IPartnerRepository partners,
    IStoredFileRepository files,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<CreatePartnerCommand, Result<PartnerId>>
{
    /// <summary>
    /// Handles the request to create a partner.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the identifier of the new partner, or
    /// <see cref="ApplicationErrorCode.PartnerThumbnailNotFound"/> when the logo is missing.
    /// </returns>
    public async Task<Result<PartnerId>> HandleAsync(
        CreatePartnerCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var available = await ContentFiles.EnsureAvailableAsync(
            files,
            command.ThumbnailId,
            null,
            ApplicationErrorCode.PartnerThumbnailNotFound,
            ct
        );
        if (available.IsFailure)
        {
            return available.Error!;
        }

        var partner = Partner.Create(
            new PartnerDetails(
                command.Name,
                command.FromDate,
                command.Tier,
                command.Website,
                command.ThumbnailId
            ),
            currentUser.RequiredId(),
            clock.UtcNow
        );
        await partners.AddAsync(partner, ct);
        return partner.Id;
    }
}
