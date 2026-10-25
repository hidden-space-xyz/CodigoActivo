using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.BlockedData;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.BlockedData;

/// <summary>
/// Tells the association's legal contact that a consultation of the blocked data was opened, once
/// it is committed, so an access nobody expected is noticed. Without a legal contact nothing is
/// sent, and a delivery failure is logged and never undoes the consultation.
/// </summary>
/// <param name="readStore">Read side that names the consultation and its officer.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="legalEntity">Legal identity of the association, with its contact email.</param>
/// <param name="emailSender">The email sender value.</param>
/// <param name="composer">Composer that renders the notice.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class BlockedDataConsultationNotification(
    IReadStore readStore,
    IQueryExecutor executor,
    LegalEntityOptions legalEntity,
    IEmailSender emailSender,
    IBlockedDataEmailComposer composer,
    ILogger<BlockedDataConsultationNotification> logger
) : IDomainEventListener<BlockedDataConsultationOpened>
{
    /// <inheritdoc />
    public async Task HandleAsync(BlockedDataConsultationOpened domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        if (string.IsNullOrWhiteSpace(legalEntity.Email))
        {
            return;
        }

        try
        {
            var id = domainEvent.ConsultationId.Value;
            var consultation = await executor.FirstOrDefaultAsync(
                BlockedDataProjections
                    .Consultations(
                        readStore.BlockedDataConsultations.Where(c => c.Id == id),
                        readStore.Users
                    )
                    .Select(c => new OpenedConsultation(
                        c.OfficerName ?? string.Empty,
                        c.Authority,
                        c.Reference,
                        c.OpenedAt
                    )),
                ct
            );
            if (consultation is null)
            {
                return;
            }

            await emailSender.SendAsync(
                composer.ConsultationOpened(
                    new EmailRecipient(legalEntity.Email, legalEntity.Name),
                    consultation
                ),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.BlockedDataConsultationNotice, ex);
        }
    }
}
