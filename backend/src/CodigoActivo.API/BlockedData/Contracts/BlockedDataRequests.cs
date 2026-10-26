using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.Common.Querying;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.API.BlockedData.Contracts;

/// <summary>
/// Request to email the code that confirms opening a consultation of the blocked data.
/// </summary>
/// <param name="CurrentPassword">Password of the signed-in user.</param>
public record BlockedDataConsultationCodeRequest([Required] [MaxLength(128)] string CurrentPassword)
{
    /// <summary>
    /// Builds the command that emails the code.
    /// </summary>
    /// <returns>The command.</returns>
    public RequestBlockedDataConsultationCodeCommand ToCommand()
    {
        return new RequestBlockedDataConsultationCodeCommand(CurrentPassword);
    }
}

/// <summary>
/// Request to open a consultation of the blocked data.
/// </summary>
/// <param name="Authority">Who the data is made available to.</param>
/// <param name="Reference">Reference of the request, such as the number of the proceedings.</param>
/// <param name="CurrentPassword">Password of the signed-in user.</param>
/// <param name="Code">Code of the second factor.</param>
public record OpenBlockedDataConsultationRequest(
    [Required] [EnumDataType(typeof(BlockedDataAuthority))] BlockedDataAuthority Authority,
    [Required] [MaxLength(BlockedDataConsultation.ReferenceMaxLength)] string Reference,
    [Required] [MaxLength(128)] string CurrentPassword,
    [Required] [MaxLength(16)] string Code
)
{
    /// <summary>
    /// Builds the command that opens the consultation.
    /// </summary>
    /// <returns>The command.</returns>
    public OpenBlockedDataConsultationCommand ToCommand()
    {
        return new OpenBlockedDataConsultationCommand(Authority, Reference, CurrentPassword, Code);
    }
}

/// <summary>
/// Request to look accounts up in a consultation of the blocked data. The text travels in the
/// body, never in the address, since it is personal data.
/// </summary>
/// <param name="Query">Identifier, email, DNI/NIE, phone number or part of the name.</param>
public record SearchBlockedDataRequest(
    [Required]
    [MinLength(SearchBlockedDataCommand.MinQueryLength)]
    [MaxLength(PageQuery.MaxTextLength)]
        string Query
)
{
    /// <summary>
    /// Builds the command that runs the search.
    /// </summary>
    /// <param name="consultationId">Identifier of the consultation.</param>
    /// <returns>The command.</returns>
    public SearchBlockedDataCommand ToCommand(Guid consultationId)
    {
        return new SearchBlockedDataCommand(BlockedDataConsultationId.From(consultationId), Query);
    }
}

/// <summary>
/// Request to take an extract of the blocked data about an account.
/// </summary>
/// <param name="AccountId">Identifier of an account a search of the consultation found.</param>
/// <param name="From">First day covered, or <see langword="null"/> for no limit.</param>
/// <param name="To">Last day covered, or <see langword="null"/> for no limit.</param>
/// <param name="Entities">Kinds of data covered; empty for every kind.</param>
public record ExportBlockedDataRequest(
    [Required] Guid AccountId,
    DateOnly? From,
    DateOnly? To,
    [MaxLength(32)] IReadOnlyList<string>? Entities
)
{
    /// <summary>
    /// Builds the command that takes the extract.
    /// </summary>
    /// <param name="consultationId">Identifier of the consultation.</param>
    /// <returns>The command.</returns>
    public ExportBlockedDataCommand ToCommand(Guid consultationId)
    {
        return new ExportBlockedDataCommand(
            BlockedDataConsultationId.From(consultationId),
            UserId.From(AccountId),
            From,
            To,
            Entities ?? []
        );
    }
}
