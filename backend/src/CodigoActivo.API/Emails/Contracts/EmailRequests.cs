using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Emails.Commands;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.API.Emails.Contracts;

/// <summary>
/// Contains the token of the unsubscribe link of a news email.
/// </summary>
/// <param name="Token">Token taken from the link.</param>
public record UnsubscribeRequest(
    [Required] [MaxLength(UnsubscribeCommand.TokenMaxLength)] string Token
)
{
    /// <summary>
    /// Builds the command that withdraws the news consent.
    /// </summary>
    /// <returns>The command.</returns>
    public UnsubscribeCommand ToCommand()
    {
        return new UnsubscribeCommand(Token, NewsConsentSource.UnsubscribeLink);
    }
}
