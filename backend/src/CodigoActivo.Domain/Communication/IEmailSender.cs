namespace CodigoActivo.Domain.Communication;

public interface IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

public interface IEmailTransport
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);

    public Task<EmailBatchResult> SendManyAsync(
        IReadOnlyList<EmailMessage> messages,
        CancellationToken ct = default
    );
}

public interface IEmailDispatcher
{
    public bool TryEnqueue(EmailMessage message);
}
