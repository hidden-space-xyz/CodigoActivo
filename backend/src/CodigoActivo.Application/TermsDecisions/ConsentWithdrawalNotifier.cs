using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.TermsDecisions;

namespace CodigoActivo.Application.TermsDecisions;

/// <summary>
/// Tells the association's legal contact about withdrawn consents, so the images or the names of
/// the people stop being used and are removed from the site and the social profiles, and records
/// the notice on each decision. The scheduled notice tells the withdrawals that wait, and the
/// removal of an event or an account tells the ones it deletes before they are gone. That removal
/// also tells the consents given that it deletes, since once blocked they can no longer be
/// consulted to cover what they allowed.
/// </summary>
/// <param name="readStore">Read side that names the event, the document and the people.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="legalEntity">Legal identity of the association, with its contact email.</param>
/// <param name="emailSender">Sender that queues the notice.</param>
/// <param name="composer">Composer that renders the notice.</param>
/// <param name="clock">Clock that tells which events have already started.</param>
public sealed class ConsentWithdrawalNotifier(
    IReadStore readStore,
    IQueryExecutor executor,
    LegalEntityOptions legalEntity,
    IEmailSender emailSender,
    IConsentWithdrawalEmailComposer composer,
    IClock clock
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

    /// <summary>
    /// Queues one email for every <see cref="MaxPerNotice"/> consents given that an erasure
    /// deletes, among the accepted decisions it takes away, about events that have already started,
    /// the only ones whose images or names may have been published. Without a legal contact nothing
    /// is sent.
    /// </summary>
    /// <param name="accepted">
    /// Accepted decisions the erasure deletes, of any kind of document.
    /// </param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the number of consents told.</returns>
    public async Task<int> NotifyErasedAsync(
        IReadOnlyList<EventTermsAcceptance> accepted,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(accepted);
        if (accepted.Count is 0 || string.IsNullOrWhiteSpace(legalEntity.Email))
        {
            return 0;
        }

        var consents = await DescribeErasedAsync(accepted, ct);
        foreach (var notice in consents.Chunk(MaxPerNotice))
        {
            await emailSender.SendAsync(
                composer.Erased(new EmailRecipient(legalEntity.Email, legalEntity.Name), notice),
                ct
            );
        }

        return consents.Count;
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

    private async Task<IReadOnlyList<ErasedConsent>> DescribeErasedAsync(
        IReadOnlyList<EventTermsAcceptance> accepted,
        CancellationToken ct
    )
    {
        var keys = accepted
            .Select(a => (a.EventId.Value, a.UserId.Value, a.TermsDocumentId.Value))
            .ToHashSet();
        var eventIds = accepted.Select(a => a.EventId.Value).Distinct().ToList();
        var userIds = accepted.Select(a => a.UserId.Value).Distinct().ToList();
        var documentIds = accepted.Select(a => a.TermsDocumentId.Value).Distinct().ToList();
        var today = clock.Today;
        var rows = await executor.ToListAsync(
            readStore
                .EventTermsAcceptances.Where(a =>
                    eventIds.Contains(a.EventId)
                    && userIds.Contains(a.UserId)
                    && documentIds.Contains(a.TermsDocumentId)
                    && a.Accepted
                    && a.TermsDocument.Kind == TermsDocumentKind.Consent
                    && a.Event.EventStartsAt <= today
                )
                .OrderBy(a => a.Event.EventStartsAt)
                .ThenBy(a => a.User.LastName)
                .ThenBy(a => a.User.FirstName)
                .Select(a => new
                {
                    a.EventId,
                    a.UserId,
                    a.TermsDocumentId,
                    Consent = new ErasedConsent(
                        a.Event.Title,
                        a.TermsDocument.Name,
                        a.User.FirstName + " " + a.User.LastName,
                        a.DecidedBy.FirstName + " " + a.DecidedBy.LastName,
                        a.DecidedAt
                    ),
                }),
            ct
        );

        return
        [
            .. rows.Where(row => keys.Contains((row.EventId, row.UserId, row.TermsDocumentId)))
                .Select(row => row.Consent),
        ];
    }
}
