using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
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
/// <param name="readStore">Read side that names the event, the document and the people.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="legalEntity">Legal identity of the association, with its contact email.</param>
/// <param name="pendingEmails">Outbox, asked whether the last notice is still waiting.</param>
/// <param name="emailSender">Sender that queues the notice.</param>
/// <param name="composer">Composer that renders the notice.</param>
public sealed class SendConsentWithdrawalNoticesCommandHandler(
    IEventTermsAcceptanceRepository acceptances,
    IReadStore readStore,
    IQueryExecutor executor,
    LegalEntityOptions legalEntity,
    IPendingEmails pendingEmails,
    IEmailSender emailSender,
    IConsentWithdrawalEmailComposer composer
) : ICommandHandler<SendConsentWithdrawalNoticesCommand, Result<int>>
{
    /// <summary>
    /// Most withdrawals one notice lists; the rest wait for the next one.
    /// </summary>
    public const int MaxPerNotice = 50;

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
            || await pendingEmails.AnyAsync(EmailTopics.ConsentWithdrawals, ct)
        )
        {
            return 0;
        }

        var pending = await acceptances.ListWithdrawalNoticesPendingAsync(MaxPerNotice, ct);
        if (pending.Count is 0)
        {
            return 0;
        }

        var withdrawals = await DescribeAsync(pending, ct);
        var notice = composer.Withdrawn(
            new EmailRecipient(legalEntity.Email, legalEntity.Name),
            withdrawals
        );
        await emailSender.SendAsync(notice with { Topic = EmailTopics.ConsentWithdrawals }, ct);

        foreach (var acceptance in pending)
        {
            acceptance.RecordWithdrawalNotice();
        }

        return pending.Count;
    }

    private async Task<IReadOnlyList<ConsentWithdrawal>> DescribeAsync(
        IReadOnlyList<EventTermsAcceptance> pending,
        CancellationToken ct
    )
    {
        var eventIds = pending.Select(a => a.EventId.Value).Distinct().ToList();
        var userIds = pending.Select(a => a.UserId.Value).Distinct().ToList();
        var documentIds = pending.Select(a => a.TermsDocumentId.Value).Distinct().ToList();
        var rows = await executor.ToListAsync(
            readStore
                .EventTermsAcceptances.Where(a =>
                    eventIds.Contains(a.EventId)
                    && userIds.Contains(a.UserId)
                    && documentIds.Contains(a.TermsDocumentId)
                )
                .Select(a => new
                {
                    a.EventId,
                    a.UserId,
                    a.TermsDocumentId,
                    Withdrawal = new ConsentWithdrawal(
                        a.Event.Title,
                        a.TermsDocument.Name,
                        a.User.FirstName + " " + a.User.LastName,
                        readStore
                            .Users.Where(u =>
                                u.Id
                                == a.Changes.OrderByDescending(c => c.Id)
                                    .Select(c => c.RecordedById)
                                    .FirstOrDefault()
                            )
                            .Select(u => u.FirstName + " " + u.LastName)
                            .FirstOrDefault()
                            ?? string.Empty,
                        a.DecidedAt
                    ),
                }),
            ct
        );
        var byKey = rows.ToDictionary(
            row => (row.EventId, row.UserId, row.TermsDocumentId),
            row => row.Withdrawal
        );

        return
        [
            .. pending.Select(a =>
                byKey[(a.EventId.Value, a.UserId.Value, a.TermsDocumentId.Value)]
            ),
        ];
    }
}
