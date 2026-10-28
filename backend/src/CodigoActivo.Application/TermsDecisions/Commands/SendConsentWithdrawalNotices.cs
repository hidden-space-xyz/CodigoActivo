using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.TermsDecisions;

namespace CodigoActivo.Application.TermsDecisions.Commands;

/// <summary>
/// Carries the request to tell the association's legal contact about the consents withdrawn since
/// the last notice.
/// </summary>
public sealed record SendConsentWithdrawalNoticesCommand : ICommand<Result<int>>, IScheduledCommand;

/// <summary>
/// Executes the command that tells the association's legal contact, in one email, about the
/// withdrawals it was not told about yet, so the images or the names of the people stop being used
/// and are removed from the site and the social profiles. A withdrawal waits in its decision until
/// the email is queued, so none is lost to a failure or a restart; while the last notice is still
/// waiting to be delivered, the new withdrawals wait for the next one, so the legal contact gets one
/// email at a time however many arrive. Without a legal contact nothing is sent and the withdrawals
/// keep waiting.
/// </summary>
/// <param name="acceptances">Repository of the decisions whose withdrawal is told.</param>
/// <param name="legalEntity">Legal identity of the association, with its contact email.</param>
/// <param name="pendingEmails">Outbox, asked whether the last notice is still waiting.</param>
/// <param name="notifier">Notifier that queues the notice and records it on each decision.</param>
public sealed class SendConsentWithdrawalNoticesCommandHandler(
    IEventTermsAcceptanceRepository acceptances,
    LegalEntityOptions legalEntity,
    IPendingEmails pendingEmails,
    ConsentWithdrawalNotifier notifier
) : ICommandHandler<SendConsentWithdrawalNoticesCommand, Result<int>>
{
    /// <summary>
    /// Handles the request to tell the legal contact about the withdrawn consents.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of withdrawals told.</returns>
    public async Task<Result<int>> HandleAsync(
        SendConsentWithdrawalNoticesCommand command,
        CancellationToken ct = default
    )
    {
        if (
            string.IsNullOrWhiteSpace(legalEntity.Email)
            || await pendingEmails.AnyAsync(EmailKind.ConsentWithdrawalNotice, ct)
        )
        {
            return 0;
        }

        var pending = await acceptances.ListWithdrawalNoticesPendingAsync(
            ConsentWithdrawalNotifier.MaxPerNotice,
            ct
        );
        return await notifier.NotifyAsync(pending, ct);
    }
}
