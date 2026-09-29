namespace CodigoActivo.Application.Abstractions.Email;

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
