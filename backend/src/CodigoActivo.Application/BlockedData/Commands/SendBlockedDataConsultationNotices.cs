using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Legal;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the request to tell the association's legal contact about the consultations of the
/// blocked data opened since the last notice.
/// </summary>
public sealed record SendBlockedDataConsultationNoticesCommand
    : ICommand<Result<int>>,
        IScheduledCommand;

/// <summary>
/// Executes the command that tells the association's legal contact, in one email, about the
/// consultations of the blocked data it was not told about yet, so an access nobody expected is
/// noticed. A consultation waits until the email is queued, so none is lost to a failure or a
/// restart; while the last notice is still waiting to be delivered, the new consultations wait for
/// the next one. Without a legal contact nothing is sent and the consultations keep waiting.
/// </summary>
/// <param name="consultations">Repository of the consultations whose opening is told.</param>
/// <param name="readStore">Read side that names the consultations and their officer.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="legalEntity">Legal identity of the association, with its contact email.</param>
/// <param name="pendingEmails">Outbox, asked whether the last notice is still waiting.</param>
/// <param name="emailSender">Sender that queues the notice.</param>
/// <param name="composer">Composer that renders the notice.</param>
public sealed class SendBlockedDataConsultationNoticesCommandHandler(
    IBlockedDataConsultationRepository consultations,
    IReadStore readStore,
    IQueryExecutor executor,
    LegalEntityOptions legalEntity,
    IPendingEmails pendingEmails,
    IEmailSender emailSender,
    IBlockedDataEmailComposer composer
) : ICommandHandler<SendBlockedDataConsultationNoticesCommand, Result<int>>
{
    /// <summary>
    /// Most consultations one notice lists; the rest wait for the next one.
    /// </summary>
    public const int MaxPerNotice = 50;

    /// <summary>
    /// Handles the request to tell the legal contact about the opened consultations.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the number of consultations told.</returns>
    public async Task<Result<int>> HandleAsync(
        SendBlockedDataConsultationNoticesCommand command,
        CancellationToken ct = default
    )
    {
        if (
            string.IsNullOrWhiteSpace(legalEntity.Email)
            || await pendingEmails.AnyAsync(EmailKind.BlockedDataConsultationNotice, ct)
        )
        {
            return 0;
        }

        var pending = await consultations.ListNoticesPendingAsync(MaxPerNotice, ct);
        if (pending.Count is 0)
        {
            return 0;
        }

        var ids = pending.Select(c => c.Id.Value).ToList();
        var opened = await executor.ToListAsync(
            BlockedDataProjections
                .Consultations(
                    readStore.BlockedDataConsultations.Where(c => ids.Contains(c.Id)),
                    readStore.Users
                )
                .Select(c => new
                {
                    c.Id,
                    Consultation = new OpenedConsultation(
                        c.OfficerName ?? string.Empty,
                        c.Authority,
                        c.Reference,
                        c.OpenedAt
                    ),
                }),
            ct
        );
        var byId = opened.ToDictionary(row => row.Id, row => row.Consultation);

        var notice = composer.ConsultationsOpened(
            new EmailRecipient(legalEntity.Email, legalEntity.Name),
            [.. pending.Select(c => byId[c.Id.Value])]
        );
        await emailSender.SendAsync(notice, ct);

        foreach (var consultation in pending)
        {
            consultation.RecordNotice();
        }

        return pending.Count;
    }
}
