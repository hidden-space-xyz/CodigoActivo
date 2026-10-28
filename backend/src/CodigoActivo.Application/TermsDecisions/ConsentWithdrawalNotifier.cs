using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.TermsDecisions;

namespace CodigoActivo.Application.TermsDecisions;

/// <summary>
/// Tells the association's legal contact about withdrawn consents, so the images or the names of
/// the people stop being used and are removed from the site and the social profiles, and records
/// the notice on each decision. The scheduled notice tells the withdrawals that wait, and the
/// removal of an event or an account tells the ones it deletes before they are gone.
/// </summary>
/// <param name="readStore">Read side that names the event, the document and the people.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="legalEntity">Legal identity of the association, with its contact email.</param>
/// <param name="emailSender">Sender that queues the notice.</param>
/// <param name="composer">Composer that renders the notice.</param>
public sealed class ConsentWithdrawalNotifier(
    IReadStore readStore,
    IQueryExecutor executor,
    LegalEntityOptions legalEntity,
    IEmailSender emailSender,
    IConsentWithdrawalEmailComposer composer
)
{
    /// <summary>
    /// Most withdrawals one notice lists.
    /// </summary>
    public const int MaxPerNotice = 50;

    /// <summary>
    /// Queues one email for every <see cref="MaxPerNotice"/> withdrawals and records the notice on
    /// each. Without a legal contact nothing is sent and the withdrawals keep waiting.
    /// </summary>
    /// <param name="pending">Withdrawals the legal contact has not been told about yet.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of withdrawals told.</returns>
    public async Task<int> NotifyAsync(
        IReadOnlyList<EventTermsAcceptance> pending,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (pending.Count is 0 || string.IsNullOrWhiteSpace(legalEntity.Email))
        {
            return 0;
        }

        foreach (var notice in pending.Chunk(MaxPerNotice))
        {
            var withdrawals = await DescribeAsync(notice, ct);
            await emailSender.SendAsync(
                composer.Withdrawn(
                    new EmailRecipient(legalEntity.Email, legalEntity.Name),
                    withdrawals
                ),
                ct
            );

            foreach (var acceptance in notice)
            {
                acceptance.RecordWithdrawalNotice();
            }
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
