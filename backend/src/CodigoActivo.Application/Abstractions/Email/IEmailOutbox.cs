namespace CodigoActivo.Application.Abstractions.Email;

/// <summary>
/// Tells what <see cref="IEmailOutbox.TryEnqueueAsync"/> did with a batch.
/// </summary>
public enum EmailEnqueueResult
{
    /// <summary>
    /// The batch was stored, or it had no recipient.
    /// </summary>
    Queued,

    /// <summary>
    /// The batch is administrator-written mail and the pending mail of that kind would pass its cap;
    /// nothing was stored.
    /// </summary>
    Full,

    /// <summary>
    /// The message is larger than the SMTP server accepts; nothing was stored.
    /// </summary>
    TooLarge,
}

/// <summary>
/// Stores outbound email so a background worker delivers it and a restart never loses a message.
/// </summary>
public interface IEmailOutbox
{
    /// <summary>
    /// Stores every recipient of the batch as one pending message, all of them or none, in a unit of
    /// work of its own that is committed before returning. A pending message with the topic of the
    /// batch to the same address is replaced. Only administrator-written mail has a cap on pending
    /// messages; every other kind is always stored, since the guard, the topics and the delivery pace
    /// already bound it.
    /// </summary>
    /// <param name="batch">Email batch to store for delivery.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result tells whether the batch was stored and, if not, why.</returns>
    public Task<EmailEnqueueResult> TryEnqueueAsync(
        EmailBatch batch,
        CancellationToken ct = default
    );
}

/// <summary>
/// Tells which email is still waiting in the outbox.
/// </summary>
public interface IPendingEmails
{
    /// <summary>
    /// Tells whether a message on a topic is still waiting to be delivered, including one being
    /// delivered right now.
    /// </summary>
    /// <param name="topic">Topic of <see cref="EmailTopics"/>.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when one is waiting.</returns>
    public Task<bool> AnyAsync(string topic, CancellationToken ct = default);
}
