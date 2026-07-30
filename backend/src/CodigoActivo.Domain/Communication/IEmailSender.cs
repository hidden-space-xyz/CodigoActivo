namespace CodigoActivo.Domain.Communication;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);

    Task<EmailBatchResult> SendManyAsync(
        IReadOnlyList<EmailMessage> messages,
        CancellationToken ct = default
    );
}
