using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Entities.Abstractions;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Represents the persisted email outbox message domain entity and its relationships. One row exists
/// per pending delivery: the table holds only email that still has to leave, so a row is deleted
/// once it is delivered or once its attempts are spent.
/// </summary>
public class EmailOutboxMessage : IdentifiableEntity
{
    /// <summary>
    /// Identifies the maximum stored length of <see cref="LastError"/>.
    /// </summary>
    public const int LastErrorMaxLength = 300;

    /// <summary>
    /// Gets or sets the identifier of the shared content this message delivers.
    /// </summary>
    public Guid ContentId { get; set; }

    /// <summary>
    /// Gets or sets the shared content this message delivers.
    /// </summary>
    public EmailOutboxContent Content { get; set; } = null!;

    /// <summary>
    /// Gets or sets the email category whose limits were applied when the message was accepted.
    /// </summary>
    public EmailKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the delivery priority derived from <see cref="Kind"/> when the message was
    /// accepted, lowest first. The worker claims by this column before the schedule, so interactive
    /// mail does not wait behind a bulk mailing.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Gets or sets the recipient address, stored as plain text because the worker needs it to
    /// address the message and it is already plain text in the user table.
    /// </summary>
    public string ToAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the recipient name.
    /// </summary>
    public string ToName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the message was accepted.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets how many delivery attempts have been started. The claim increments it together
    /// with the lease, so an attempt counts even when the process delivering it dies, and the
    /// attempts of a message are always spent after the configured maximum.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp from which the message may be attempted again.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp until which a worker holds the message. A row whose lease has
    /// passed is claimable again, so a process that died mid-send neither loses the message nor
    /// blocks it forever.
    /// </summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>
    /// Gets or sets the truncated diagnostic left by the most recent failure.
    /// </summary>
    public string? LastError { get; set; }
}
