using CodigoActivo.Domain.Communication;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Diagnostics;

/// <summary>
/// Declares the operational events of the infrastructure layer: outbound email trouble and failed
/// background runs. Templates carry counts, enum values and exceptions, never identifiers,
/// addresses, names or values typed by the client.
/// </summary>
public static partial class InfrastructureLog
{
    /// <summary>
    /// Records a failed delivery attempt that will be retried.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="attempts">Attempts already spent on the message.</param>
    /// <param name="kind">Email category of the message.</param>
    /// <param name="exception">Failure reported by the transport.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Attempt {Attempts} to deliver a {Kind} email failed and will be retried"
    )]
    public static partial void EmailDeliveryAttemptFailed(
        this ILogger logger,
        int attempts,
        EmailKind kind,
        Exception exception
    );

    /// <summary>
    /// Records that a message ran out of attempts and was dropped from the outbox.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="kind">Email category of the message.</param>
    /// <param name="attempts">Attempts spent before giving up.</param>
    /// <param name="exception">Last failure reported by the transport, when there is one.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Gave up on a {Kind} email after {Attempts} failed delivery attempts and removed it from the outbox"
    )]
    public static partial void EmailDeliveryGaveUp(
        this ILogger logger,
        EmailKind kind,
        int attempts,
        Exception? exception
    );

    /// <summary>
    /// Records that a failed attempt could not be written back, so the message waits for its lease
    /// to expire instead.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="attempts">Attempts already spent on the message.</param>
    /// <param name="kind">Email category of the message.</param>
    /// <param name="exception">Failure raised while recording the attempt.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not record the failed attempt {Attempts} of a {Kind} email; its lease will expire and it will be retried"
    )]
    public static partial void EmailAttemptNotRecorded(
        this ILogger logger,
        int attempts,
        EmailKind kind,
        Exception exception
    );

    /// <summary>
    /// Records that the messages whose attempts are spent could not be removed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while removing the rows.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not remove the email whose delivery attempts are spent; the next run will retry"
    )]
    public static partial void ExhaustedEmailNotRemoved(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the stored content of delivered email could not be cleared.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while clearing the content.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not clear the stored content of delivered email; the next run will retry"
    )]
    public static partial void EmailContentSweepFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that a whole delivery run failed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "An email outbox delivery run failed; the next run will retry"
    )]
    public static partial void EmailOutboxRunFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that shutdown did not leave enough time to finish the deliveries in flight.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="drain">Time the worker was given to finish.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The email outbox did not finish delivering within {Drain}; the messages in flight stay stored"
    )]
    public static partial void EmailOutboxDrainIncomplete(this ILogger logger, TimeSpan drain);

    /// <summary>
    /// Records that the outbox is full, so an automatic email was held back.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="capacity">Pending messages the outbox accepts.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The outbound email outbox is full at {Capacity} pending messages; an email was held back"
    )]
    public static partial void EmailOutboxFull(this ILogger logger, int capacity);

    /// <summary>
    /// Records that the per-recipient quota is holding automatic mail.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The per-recipient outbound email quota is now holding mail; an email was not sent"
    )]
    public static partial void RecipientEmailQuotaReached(this ILogger logger);

    /// <summary>
    /// Records that the global outbound budget is close to holding automatic mail.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="remaining">Messages left before automatic mail is held.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The global outbound email budget is running low with {Remaining} messages left before automatic mail is held"
    )]
    public static partial void GlobalEmailBudgetLow(this ILogger logger, int remaining);

    /// <summary>
    /// Records that the global outbound budget is spent and automatic mail is being denied.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The global outbound email budget is exhausted and an email was denied; automatic mail is held "
            + "until the budget refills, admin-written email is unaffected"
    )]
    public static partial void GlobalEmailBudgetExhausted(this ILogger logger);

    /// <summary>
    /// Records that the quota stopped tracking new recipients individually.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="limit">Recipients the quota tracks at once.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The outbound email quota already tracks {Limit} recipients, so new addresses are accounted against the global "
            + "budget only"
    )]
    public static partial void EmailRecipientTrackingSaturated(this ILogger logger, int limit);

    /// <summary>
    /// Records that the periodic removal of expired session rows failed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The expired session cleanup run failed; the next run will retry"
    )]
    public static partial void ExpiredSessionCleanupFailed(
        this ILogger logger,
        Exception exception
    );
}
