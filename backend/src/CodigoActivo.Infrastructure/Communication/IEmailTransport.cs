using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication;

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
}
