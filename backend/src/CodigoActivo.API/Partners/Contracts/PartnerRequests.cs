using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Partners.Commands;
using CodigoActivo.Domain.Files;
using CodigoActivo.Domain.Partners;

namespace CodigoActivo.API.Partners.Contracts;

/// <summary>
/// Contains the client-supplied data used to create a partner.
/// </summary>
/// <param name="Name">Name of the partner.</param>
/// <param name="FromDate">Day the collaboration started; today at the latest.</param>
/// <param name="Tier">Sponsorship tier, zero or more.</param>
/// <param name="Website">Website of the partner, if any.</param>
/// <param name="ThumbnailId">Identifier of the uploaded logo file, which must exist.</param>
public record CreatePartnerRequest(
    [Required] [MaxLength(200)] string Name,
    [Required] DateOnly? FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [MaxLength(500)] string? Website,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Builds the command that creates the partner.
    /// </summary>
    /// <returns>The command.</returns>
    public CreatePartnerCommand ToCommand()
    {
        return new CreatePartnerCommand(
            Name,
            FromDate.GetValueOrDefault(),
            Tier,
            Website,
            StoredFileId.From(ThumbnailId)
        );
    }
}

/// <summary>
/// Contains the client-supplied data that replaces every editable field of a partner.
/// </summary>
/// <param name="Name">Name of the partner.</param>
/// <param name="FromDate">Day the collaboration started; today at the latest.</param>
/// <param name="Tier">Sponsorship tier, zero or more.</param>
/// <param name="Website">Website of the partner, if any.</param>
/// <param name="ThumbnailId">Identifier of the uploaded logo file, which must exist.</param>
public record UpdatePartnerRequest(
    [Required] [MaxLength(200)] string Name,
    [Required] DateOnly? FromDate,
    [Range(0, int.MaxValue)] int Tier,
    [MaxLength(500)] string? Website,
    Guid ThumbnailId
)
{
    /// <summary>
    /// Builds the command that updates the partner.
    /// </summary>
    /// <param name="partnerId">Identifier of the partner.</param>
    /// <returns>The command.</returns>
    public UpdatePartnerCommand ToCommand(PartnerId partnerId)
    {
        return new UpdatePartnerCommand(
            partnerId,
            Name,
            FromDate.GetValueOrDefault(),
            Tier,
            Website,
            StoredFileId.From(ThumbnailId)
        );
    }
}
