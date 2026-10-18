using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.Emails.Commands;

/// <summary>
/// Carries the token of the unsubscribe link of a news email.
/// </summary>
/// <param name="Token">Token taken from the link.</param>
/// <param name="Source">
/// Whether the holder followed the link in the email or pressed the unsubscribe button of their
/// mail client, recorded with the withdrawal.
/// </param>
public sealed record UnsubscribeCommand(
    [property: Required, MaxLength(UnsubscribeCommand.TokenMaxLength), NotBlank] string Token,
    [property: AllowedValues(
        NewsConsentSource.UnsubscribeLink,
        NewsConsentSource.OneClickUnsubscribe
    )]
        NewsConsentSource Source
) : ICommand<Result>
{
    /// <summary>
    /// Maximum length of the token.
    /// </summary>
    public const int TokenMaxLength = 512;
}

/// <summary>
/// Withdraws the news consent of the account an unsubscribe link names, without a session:
/// the link is the proof that whoever follows it reads that account's email.
/// </summary>
/// <param name="tokens">Reads the token of the link.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
/// <param name="auditSubject">Names the account the link found for the audit log.</param>
public sealed class UnsubscribeCommandHandler(
    IUnsubscribeTokens tokens,
    IUserRepository users,
    IClock clock,
    IAuditSubject auditSubject
) : ICommandHandler<UnsubscribeCommand, Result>
{
    /// <summary>
    /// Handles the request to unsubscribe. Following the link again, or after the account was
    /// erased, succeeds without changing anything.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is success, or an application error when the link is not valid.</returns>
    public async Task<Result> HandleAsync(
        UnsubscribeCommand command,
        CancellationToken ct = default
    )
    {
        if (!tokens.TryRead(command.Token, out var userId))
        {
            return Error.Validation(ApplicationErrorCode.EmailUnsubscribeLinkInvalid);
        }

        auditSubject.Name(userId);
        var user = await users.GetByIdAsync(userId, ct);
        user?.WithdrawNewsConsent(command.Source, clock.UtcNow);
        return Result.Success();
    }
}
