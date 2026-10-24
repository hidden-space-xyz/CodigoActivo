using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Common.Mapping;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Application.Legal.Commands;

namespace CodigoActivo.API.Legal.Contracts;

/// <summary>
/// Contains the client-supplied content of a new version of a legal policy.
/// </summary>
/// <param name="Content">Rich-text document holding text only; images are refused.</param>
public record PublishLegalPolicyRequest(
    [Required] [MaxLength(RichTextAllowlist.MaxJsonLength)] string Content
)
{
    /// <summary>
    /// Builds the command that publishes the policy.
    /// </summary>
    /// <param name="kind">Which policy to publish.</param>
    /// <returns>The command.</returns>
    public PublishLegalPolicyCommand ToCommand(LegalPolicyKind kind)
    {
        return new PublishLegalPolicyCommand(kind.ToDomain(), Content);
    }
}
