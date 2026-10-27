using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Hands one addressed email to the mail server. The outbox deliverer calls it for every claimed
/// message, and whatever it throws is a failed attempt that is retried or given up.
/// </summary>
public interface IEmailTransport
{
    /// <summary>
    /// Delivers the message right away, with no quota, queue or retry of its own; a failure leaves as
    /// an exception.
    /// </summary>
    /// <param name="message">Email message to deliver.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
