using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CodigoActivo.Application.Abstractions.Auditing;
using Serilog.Events;
using Serilog.Parsing;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Carries an <see cref="AuditRecord"/> through the Serilog pipeline of the audit log and turns it
/// into the row <see cref="AuditLogSink"/> stores. The record travels whole as the only property
/// of the event, so nothing is converted twice.
/// </summary>
public static class AuditLogEvents
{
    /// <summary>
    /// Identifies the property that holds the record.
    /// </summary>
    public const string RecordProperty = "Record";

    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse(
        "{Record}"
    );

    /// <summary>
    /// Builds the event of a record.
    /// </summary>
    /// <param name="record">What the command did.</param>
    /// <param name="occurredAt">When the command ended.</param>
    /// <param name="activity">Activity the command ran in, if any.</param>
    /// <returns>The event to queue.</returns>
    public static LogEvent ToEvent(
        AuditRecord record,
        DateTimeOffset occurredAt,
        Activity? activity
    )
    {
        ArgumentNullException.ThrowIfNull(record);

        return new LogEvent(
            occurredAt,
            LogEventLevel.Information,
            exception: null,
            Template,
            [new LogEventProperty(RecordProperty, new ScalarValue(record))],
            activity?.TraceId ?? default,
            activity?.SpanId ?? default
        );
    }

    /// <summary>
    /// Builds the stored row of an event.
    /// </summary>
    /// <param name="logEvent">Event built by <see cref="ToEvent"/>.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentException">The event carries no audit record.</exception>
    public static AuditLogEntry ToEntry(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        if (
            !logEvent.Properties.TryGetValue(RecordProperty, out var value)
            || value is not ScalarValue { Value: AuditRecord record }
        )
        {
            throw new ArgumentException("The event carries no audit record.", nameof(logEvent));
        }

        var occurredAt = logEvent.Timestamp.ToUniversalTime();
        return new AuditLogEntry
        {
            Id = Guid.CreateVersion7(occurredAt),
            OccurredAt = occurredAt,
            ActorType = record.ActorType.ToString(),
            ActorId = record.ActorId?.Value,
            ClientIp = record.ClientAddress,
            Action = record.Action.ToString(),
            EntityType = record.Entity?.Type.ToString(),
            EntityId = record.Entity?.Id,
            Related = record.Related.Count is 0 ? null : RelatedJson(record.Related),
            Outcome = record.Outcome.ToString(),
            ErrorCode = LogText.CleanOrNull(record.ErrorCode, AuditLogEntry.NameMaxLength),
            TraceId =
                logEvent.TraceId is { } trace && trace != default ? trace.ToHexString() : null,
        };
    }

    private static string RelatedJson(IReadOnlyList<AuditEntity> related)
    {
        return Json(writer =>
        {
            writer.WriteStartArray();
            foreach (var entity in related)
            {
                writer.WriteStartObject();
                writer.WriteString("type", entity.Type.ToString());
                writer.WriteString("id", entity.Id);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
