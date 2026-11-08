using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Represents an outbox message addressed to one recipient. One row exists per pending delivery:
/// the table holds only email that still has to leave, so a row is deleted once it is delivered,
/// rejected for good, replaced by a newer message on its topic or expired.
/// </summary>
public class EmailOutboxMessage
{
    /// <summary>
    /// Gets the unique identifier, fixed when the row is created.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Identifies the maximum stored length of <see cref="LastError"/>; a longer diagnostic is cut.
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
    /// Gets or sets the email category, which sets <see cref="Priority"/> and names the message in
    /// the log.
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
    /// Gets or sets the unsubscribe link of the recipient of a news email, which delivery
    /// puts in the place its shared content leaves for it. It is stored as plain text because it
    /// only withdraws the news consent of that recipient.
    /// </summary>
    public string? UnsubscribeUrl { get; set; }

    /// <summary>
    /// Gets or sets the address the mail client of the recipient of a news email posts to
    /// when they press its unsubscribe button, which delivery sends in the
    /// <c>List-Unsubscribe</c> header. It is stored as plain text for the same reason as
    /// <see cref="UnsubscribeUrl"/>.
    /// </summary>
    public string? OneClickUnsubscribeUrl { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the message was accepted.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets what the message is about, from
    /// <see cref="CodigoActivo.Application.Abstractions.Email.EmailTopics"/>: storing a message on
    /// the same topic to the same address replaces this one while it waits. <see langword="null"/>
    /// for messages nothing replaces.
    /// </summary>
    public string? Topic { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp after which the message is no longer worth delivering and is
    /// removed; <see langword="null"/> for the notices that must arrive however late.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets how many delivery attempts have been started. The claim increments it and
    /// leaves the message due, so an attempt counts even when the process delivering it dies and
    /// the next start claims the message again at once. A message the worker hands back untried,
    /// because the delivery paused, its recipient must wait or the host is stopping, gets its
    /// attempt back.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp from which the message may be claimed: the time it was
    /// accepted, after each failed attempt the end of its retry delay and, when it is handed back
    /// untried, the time it may be tried again.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets the exception type and message of the most recent failed attempt that was
    /// rescheduled, cut to <see cref="LastErrorMaxLength"/>; <see langword="null"/> until one
    /// fails.
    /// </summary>
    public string? LastError { get; set; }
}
