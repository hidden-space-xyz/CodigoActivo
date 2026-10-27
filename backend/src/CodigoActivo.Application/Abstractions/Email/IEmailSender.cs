namespace CodigoActivo.Application.Abstractions.Email;

/// <summary>
/// Accepts automatic email to one addressee for delivery.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Stores the message in the outbox, from which a background worker delivers it; it returns
    /// before the email is sent. A code is first checked against the email guard; every other kind
    /// is always stored and paced when it is delivered.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="EmailRateLimitedException">
    /// The email guard refused a code; nothing was stored.
    /// </exception>
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
