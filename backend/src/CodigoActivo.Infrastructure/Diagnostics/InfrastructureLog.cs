using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Accounts;
using CodigoActivo.Infrastructure.Communication;
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
    /// Records that the server refused a message for good, or that its stored content cannot be read,
    /// so it was dropped from the outbox.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="kind">Email category of the message.</param>
    /// <param name="failure">Whose fault the refusal was.</param>
    /// <param name="exception">Refusal reported by the transport or the protector.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "A {Kind} email was refused for good ({Failure}) and removed from the outbox"
    )]
    public static partial void EmailRejected(
        this ILogger logger,
        EmailKind kind,
        EmailDeliveryFailure failure,
        Exception exception
    );

    /// <summary>
    /// Records that messages of a kind expired before they could be delivered and were removed.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="count">Messages removed.</param>
    /// <param name="kind">Email category of the messages.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{Count} {Kind} emails expired before they could be delivered and were removed from the outbox"
    )]
    public static partial void EmailsExpired(this ILogger logger, int count, EmailKind kind);

    /// <summary>
    /// Records that notices which never expire have waited longer than they should. The deliverer
    /// checks it at most once per alert interval of the email guard.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="count">Overdue messages.</param>
    /// <param name="hours">Age, in hours, past which a message is overdue.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "{Count} emails that must be delivered have waited in the outbox for more than {Hours} hours"
    )]
    public static partial void EmailsOverdue(this ILogger logger, int count, int hours);

    /// <summary>
    /// Records that the SMTP server cannot take mail and the delivery paused. The deliverer logs it
    /// at most once per alert interval of the email guard, since the pause repeats while the cause
    /// lasts.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="minutes">Length of the pause, in minutes.</param>
    /// <param name="exception">Failure that made the delivery pause.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The SMTP server cannot take mail; email delivery paused for {Minutes} min without spending any attempt"
    )]
    public static partial void EmailDeliveryPaused(
        this ILogger logger,
        int minutes,
        Exception exception
    );

    /// <summary>
    /// Records that the daily limit of the SMTP provider holds part of the mail back. The deliverer
    /// logs it at most once per alert interval of the email guard.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="limit">Messages the provider accepts per day.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The daily limit of {Limit} messages of the SMTP provider is nearly spent; only codes leave until it refills"
    )]
    public static partial void EmailDailyLimitReached(this ILogger logger, int limit);

    /// <summary>
    /// Records that the messages handed to the SMTP server could not be added to the daily count, so
    /// the count is lower than it should be until the window passes.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while writing the count.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not add the delivered emails to the daily count of the SMTP provider"
    )]
    public static partial void EmailDeliveryCountNotRecorded(
        this ILogger logger,
        Exception exception
    );

    /// <summary>
    /// Records that the result of a failed attempt could not be written back, so the message waits
    /// for its lease to expire instead and is then retried.
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
    /// Records that the expired messages could not be removed. The deliverer logs it at most once per
    /// alert interval of the email guard.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while removing the rows.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not remove the expired email; the next run will retry"
    )]
    public static partial void ExpiredEmailNotRemoved(this ILogger logger, Exception exception);

    /// <summary>
    /// Records that the stored content no pending message references any more, left by delivered,
    /// replaced or expired email, could not be cleared.
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
    /// Records that the budget of the mail anyone can ask for is spent and such mail is being denied.
    /// The send limiter raises it at most once per alert interval of the email guard.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The outbound budget of verification and password-reset email is exhausted and an email was denied; "
            + "login codes and notices are unaffected"
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
        Message = "The outbound email quota already tracks {Limit} recipients, so new addresses are not limited on their own"
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
    /// Records that the periodic notices to the association's legal contact failed; what they tell
    /// keeps waiting for the next run.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that ended the run.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The legal contact notice run failed; the next run will retry"
    )]
    public static partial void LegalNoticeRunFailed(this ILogger logger, Exception exception);

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
