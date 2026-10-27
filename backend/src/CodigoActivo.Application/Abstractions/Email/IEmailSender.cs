namespace CodigoActivo.Application.Abstractions.Email;

/// <summary>
/// Accepts email to one addressee for delivery, within the sending limits of the application.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Checks the sending limits for the message and stores it in the outbox, from which a
    /// background worker delivers it; it returns before the email is sent.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EmailRateLimitedException">
    /// A sending limit refused the message or the outbox is full; nothing was stored.
    /// </exception>
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
