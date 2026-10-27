using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.TermsDecisions;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.TermsDecisions;

/// <summary>
/// Tells the association's legal contact that a consent was withdrawn, once the withdrawal is
/// committed, so the images or the name of the person stop being used and are removed from the
/// site and the social profiles. Without a legal contact nothing is sent, and a delivery failure is
/// logged and never undoes the withdrawal.
/// </summary>
/// <param name="readStore">Read side that names the event, the document and the people.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="legalEntity">Legal identity of the association, with its contact email.</param>
/// <param name="emailSender">Sender that delivers the notice.</param>
/// <param name="composer">Composer that renders the notice.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ConsentWithdrawalNotification(
    IReadStore readStore,
    IQueryExecutor executor,
    LegalEntityOptions legalEntity,
    IEmailSender emailSender,
    IConsentWithdrawalEmailComposer composer,
    ILogger<ConsentWithdrawalNotification> logger
) : IDomainEventListener<TermsConsentWithdrawn>
{
    /// <inheritdoc />
    public async Task HandleAsync(TermsConsentWithdrawn domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (string.IsNullOrWhiteSpace(legalEntity.Email))
        {
            return;
        }

        try
        {
            var eventId = domainEvent.EventId.Value;
            var userId = domainEvent.UserId.Value;
            var termsDocumentId = domainEvent.TermsDocumentId.Value;
            var recordedById = domainEvent.RecordedById.Value;
            var withdrawal = await executor.FirstOrDefaultAsync(
                readStore
                    .EventTermsAcceptances.Where(a =>
                        a.EventId == eventId
                        && a.UserId == userId
                        && a.TermsDocumentId == termsDocumentId
                    )
                    .Select(a => new ConsentWithdrawal(
                        a.Event.Title,
                        a.TermsDocument.Name,
                        a.User.FirstName + " " + a.User.LastName,
                        readStore
                            .Users.Where(u => u.Id == recordedById)
                            .Select(u => u.FirstName + " " + u.LastName)
                            .FirstOrDefault()
                            ?? string.Empty,
                        a.DecidedAt
                    )),
                ct
            );
            if (withdrawal is null)
            {
                return;
            }

            await emailSender.SendAsync(
                composer.Withdrawn(
                    new EmailRecipient(legalEntity.Email, legalEntity.Name),
                    withdrawal
                ),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.ConsentWithdrawalNotice, ex);
        }
    }
}
