using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Accounts;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Diagnostics;

/// <summary>
/// Declares the operational events of the infrastructure layer: outbound email trouble, failed
/// background runs and log table maintenance, failed effects of committed changes, unreadable
/// authenticator keys and data history entries, data protection keys left out of the key ring and
/// disposable email domain list refreshes. Templates carry counts, enum values, handler type names
/// and exceptions, never identifiers, addresses, names or values typed by the client.
/// </summary>
public static partial class InfrastructureLog
{
    /// <summary>
    /// Records a failed delivery attempt that will be retried. The deliverer logs it at most once
    /// per alert interval of the email guard, since the failure repeats while its cause lasts.
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
    /// <param name="exception">
    /// Failure of the last attempt, or <see langword="null"/> when the message was abandoned during
    /// its last attempt and removed once its lease expired.
    /// </param>
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
    /// Records that the result of a failed attempt could not be written back, so the message waits
    /// for its lease to expire instead: it is then retried or, when that was its last attempt,
    /// removed and logged as given up.
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
    /// Records that the messages whose attempts are spent could not be removed. The deliverer logs
    /// it at most once per alert interval of the email guard.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while removing the rows.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not remove the email whose delivery attempts are spent; the next run will retry"
    )]
    public static partial void ExhaustedEmailNotRemoved(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the stored content no pending message references any more, left by delivered
    /// or given-up email, could not be cleared.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while clearing the content.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not clear the stored content of delivered email; the next run will retry"
    )]
    public static partial void EmailContentSweepFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that a whole delivery run failed. The delivery worker logs it at most once per alert
    /// interval of the email guard, since an unreachable dependency fails every poll.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "An email outbox delivery run failed; the next run will retry"
    )]
    public static partial void EmailOutboxRunFailed(this ILogger logger, Exception exception);

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
    /// Records that the global outbound budget is spent and automatic mail is being denied. The
    /// send limiter raises it at most once per alert interval of the email guard.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The global outbound email budget is exhausted and an email was denied; automatic mail is held "
            + "until the budget refills, admin-written email is unaffected"
    )]
    public static partial void GlobalEmailBudgetExhausted(this ILogger logger);

    /// <summary>
    /// Records that the quota stopped tracking new recipients individually. The send limiter
    /// raises it at most once per alert interval of the email guard.
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

    /// <summary>
    /// Records that the periodic purge of the data history failed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The data history purge run failed; the next run will retry"
    )]
    public static partial void DataHistoryPurgeFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that an entry of the data history could not be read: it could not be decrypted,
    /// because it was altered or its key is no longer in the Data Protection key ring, or its
    /// decrypted record is not valid JSON. The entry is still listed, without its content.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that prevented reading it.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "A data history entry could not be decrypted; it was altered or its key was lost"
    )]
    public static partial void DataHistoryEntryUnreadable(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the periodic erasure of the accounts nobody verified in time failed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The unverified account purge run failed; the next run will retry"
    )]
    public static partial void UnverifiedAccountPurgeFailed(
        this ILogger logger,
        Exception exception
    );

    /// <summary>
    /// Records that the periodic erasure of the dependents who came of age, or the notices that
    /// announce it, failed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The adult dependent purge run failed; the next run will retry"
    )]
    public static partial void AdultDependentPurgeFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the periodic deletion of the uploads no content used in time failed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The unused upload purge run failed; the next run will retry"
    )]
    public static partial void UnusedFilePurgeFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the partitions of the log tables could not be prepared or purged, which leaves
    /// expired entries in place or the next days without a partition until the next run.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The log table maintenance failed; the next run will retry"
    )]
    public static partial void LogMaintenanceFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the invented history of a demo deployment could not be written to the log
    /// tables, which leaves its diagnostic and audit pages without it until a later start.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that stopped the writing.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The demo log history could not be written; the next start will retry"
    )]
    public static partial void DemoLogHistoryFailed(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the disposable email domain list could not be downloaded.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised by the source, the transport or the timeout.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The disposable email domain list could not be downloaded; the last valid list stays in use and "
            + "the download will be retried"
    )]
    public static partial void DisposableEmailDomainDownloadFailed(
        this ILogger logger,
        Exception exception
    );

    /// <summary>
    /// Records that a downloaded disposable email domain list failed validation.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="kind">Check the downloaded list failed.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The downloaded disposable email domain list was rejected as {Kind}; the last valid list stays in use "
            + "and the download will be retried"
    )]
    public static partial void DisposableEmailDomainListRejected(
        this ILogger logger,
        DisposableEmailDomainListRejection kind
    );

    /// <summary>
    /// Records that a valid disposable email domain list could not be stored.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while storing the list.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The disposable email domain list could not be stored; the last valid list stays in use and the "
            + "next run will retry"
    )]
    public static partial void DisposableEmailDomainListNotStored(
        this ILogger logger,
        Exception exception
    );

    /// <summary>
    /// Records that keys not encrypted with the data protection root key were left out of the
    /// Data Protection key ring, which means someone wrote to the key volume.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="count">Keys left out.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Ignored {Count} data protection keys not encrypted with DATA_PROTECTION_KEY"
    )]
    public static partial void DataProtectionKeysIgnored(this ILogger logger, int count);

    /// <summary>
    /// Records that an effect of a committed change failed, so that effect is lost while the
    /// change and the other effects stand.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="effect">Short type name, without namespace, of the listener or handler that failed.</param>
    /// <param name="exception">Failure raised by the handler.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The effect {Effect} of a committed change failed; the change stays committed and the other "
            + "effects still run"
    )]
    public static partial void CommittedEffectFailed(
        this ILogger logger,
        string effect,
        Exception exception
    );

    /// <summary>
    /// Records that a stored authenticator key could not be decrypted, which stops every code of
    /// that account from being accepted.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while decrypting the key.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "A stored authenticator key could not be decrypted; the data protection keys may have changed"
    )]
    public static partial void AuthenticatorKeyUnreadable(this ILogger logger, Exception exception);
}
