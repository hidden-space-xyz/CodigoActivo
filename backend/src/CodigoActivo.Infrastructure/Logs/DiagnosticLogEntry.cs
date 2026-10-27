namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Represents one technical log event the API stored in <c>diagnostic_log_entries</c>. The table
/// is partitioned by day of <see cref="OccurredAt"/> and only <see cref="DiagnosticLogSink"/>
/// writes it; this type exists to declare its schema, so it never goes through a repository.
/// </summary>
public class DiagnosticLogEntry
{
    /// <summary>
    /// Identifies the maximum stored length of <see cref="Category"/> and <see cref="EventName"/>.
    /// </summary>
    public const int NameMaxLength = 256;

    /// <summary>
    /// Identifies the maximum stored length of <see cref="Message"/>.
    /// </summary>
    public const int MessageMaxLength = 4_000;

    /// <summary>
    /// Identifies the maximum stored length of <see cref="Exception"/>.
    /// </summary>
    public const int ExceptionMaxLength = 32_000;

    /// <summary>
    /// Identifies the stored length of <see cref="TraceId"/>, the hexadecimal W3C trace id.
    /// </summary>
    public const int TraceIdLength = 32;

    /// <summary>
    /// Gets the version 7 identifier, which carries the instant it was created for.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the UTC instant of the event, which also chooses its daily partition.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets the severity, from 0 (trace) to 5 (critical).
    /// </summary>
    public short Level { get; init; }

    /// <summary>
    /// Gets the logger category, usually the type that wrote the event.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the declared event, when it has one.
    /// </summary>
    public string? EventName { get; init; }

    /// <summary>
    /// Gets the rendered message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the exception attached to the event, with its stack trace.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Gets the trace id of the request or activity the event was written in.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the values of the message template as a JSON object, or <see langword="null"/> when the
    /// template has none.
    /// </summary>
    public string? Properties { get; init; }
}
