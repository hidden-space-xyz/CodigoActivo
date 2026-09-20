using CodigoActivo.Domain.Communication;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Diagnostics;

/// <summary>
/// Declares the operational events of the application layer: only failures an operator has to act
/// on. Templates carry counts, enum values and exceptions, never identifiers, addresses, names or
/// values typed by the client.
/// </summary>
public static partial class ApplicationLog
{
    /// <summary>
    /// Records that an automatic email could not be handed to the outbound transport.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="kind">Email category that failed to leave.</param>
    /// <param name="exception">Failure that stopped the send.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Sending a {Kind} email failed")]
    public static partial void EmailSendFailed(
        this ILogger logger,
        EmailKind kind,
        Exception exception
    );

    /// <summary>
    /// Records that an administrator-written email could not be stored for delivery.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="count">Number of recipients the batch was queued for.</param>
    /// <param name="exception">Failure that stopped the batch from being stored.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Could not queue a manual email for {Count} recipients"
    )]
    public static partial void ManualEmailQueueFailed(
        this ILogger logger,
        int count,
        Exception exception
    );

    /// <summary>
    /// Records that the outbox had no room left for an administrator-written email.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="count">Number of recipients the batch was queued for.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The outbound email outbox has no room for a manual email to {Count} recipients"
    )]
    public static partial void ManualEmailOutboxFull(this ILogger logger, int count);

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

    /// <summary>
    /// Records that best-effort cleanup left unreferenced file data behind, which only wastes
    /// storage.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure raised while removing the orphan data.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Orphan file cleanup failed")]
    public static partial void OrphanFileCleanupFailed(this ILogger logger, Exception exception);
}
