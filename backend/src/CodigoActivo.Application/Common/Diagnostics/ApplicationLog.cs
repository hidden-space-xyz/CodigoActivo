using CodigoActivo.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Common.Diagnostics;

/// <summary>
/// Declares the operational events of the application layer: only failures an operator has to act
/// on. Templates carry counts, enum values and exceptions, never identifiers, addresses, names or
/// values typed by the client.
/// </summary>
public static partial class ApplicationLog
{
    /// <summary>
    /// Records that the content of a deleted file could not be removed from storage, so it stays on
    /// the volume until an operator removes it.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="exception">Failure that stopped the removal.</param>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Removing the content of a deleted file from storage failed; it stays on the volume"
    )]
    public static partial void StoredFileContentDeleteFailed(
        this ILogger logger,
        Exception exception
    );

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
    /// Records that a command or query took longer than expected.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="useCase">Type name of the command or query.</param>
    /// <param name="elapsedMilliseconds">Time it took.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{UseCase} took {ElapsedMilliseconds} ms")]
    public static partial void SlowUseCase(
        this ILogger logger,
        string useCase,
        long elapsedMilliseconds
    );

    /// <summary>
    /// Records that the audit entry of a command could not be built or queued, so the audit log
    /// misses it; the command itself ended as it would have without auditing.
    /// </summary>
    /// <param name="logger">Logger used to record operational diagnostics.</param>
    /// <param name="useCase">Type name of the command.</param>
    /// <param name="exception">Failure raised while recording the entry.</param>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The audit entry of a {UseCase} could not be recorded"
    )]
    public static partial void AuditEntryLost(
        this ILogger logger,
        string useCase,
        Exception exception
    );
}
