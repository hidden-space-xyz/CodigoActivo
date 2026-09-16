namespace CodigoActivo.Domain.Communication;

/// <summary>
/// Sends email through the configured email sender transport.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends the prepared email sender message.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// Defines the operations required to work with email transport.
/// </summary>
public interface IEmailTransport
{
    /// <summary>
    /// Sends the prepared email transport message.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);

    /// <summary>
    /// Sends the many message to its recipients.
    /// </summary>
    /// <param name="messages">Email messages to deliver as a batch.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains an email batch.</returns>
    public Task<EmailBatchResult> SendManyAsync(
        IReadOnlyList<EmailMessage> messages,
        CancellationToken ct = default
    );
}

/// <summary>
/// Dispatches email work through the configured queue.
/// </summary>
public interface IEmailDispatcher
{
    /// <summary>
    /// Attempts to add the email to the bounded delivery queue.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <returns><see langword="true"/> when the condition is met; otherwise, <see langword="false"/>.</returns>
    public bool TryEnqueue(EmailMessage message);
}
