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
}

/// <summary>
/// Stores outbound email so a background worker delivers it and a restart never loses a message.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>
    /// Stores every recipient of the batch as one pending message, all of them or none, in a unit of
    /// work of its own that is committed before returning.
    /// </summary>
    /// <param name="batch">Email batch to store for delivery.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when the batch was stored; <see langword="false"/>
    /// when the pending message cap would be exceeded, in which case nothing was stored.
    /// </returns>
    public Task<bool> TryEnqueueAsync(EmailBatch batch, CancellationToken ct = default);
}
