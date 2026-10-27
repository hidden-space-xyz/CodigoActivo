namespace CodigoActivo.Application.Abstractions.Email;

/// <summary>
/// Stores outbound email so a background worker delivers it and a restart never loses a message.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>
    /// Stores every recipient of the batch as one pending message, all of them or none, in a unit of
    /// work of its own that is committed before returning. The cap on pending messages holds back
    /// every kind except the login code, the password reset and the account verification, which are
    /// stored past it; an empty batch stores nothing and counts as stored.
    /// </summary>
    /// <param name="batch">Email batch to store for delivery.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is <see langword="true"/> when the batch was stored; <see langword="false"/>
    /// when the pending message cap would be exceeded, in which case nothing was stored.
    /// </returns>
    public Task<bool> TryEnqueueAsync(EmailBatch batch, CancellationToken ct = default);
}
