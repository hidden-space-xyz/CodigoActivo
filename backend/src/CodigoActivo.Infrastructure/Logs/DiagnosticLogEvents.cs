using System.Globalization;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;

namespace CodigoActivo.Infrastructure.Logs;

/// <summary>
/// Turns a Serilog event into the row <see cref="DiagnosticLogSink"/> stores. Only what the event
/// itself declares is kept: the values of its message template, the name of its event id and its
/// category. Properties added by logging scopes, such as the requested path, are dropped.
/// </summary>
public static class DiagnosticLogEvents
{
    /// <summary>
    /// Identifies the property that holds the logger category.
    /// </summary>
    public const string CategoryProperty = "SourceContext";

    /// <summary>
    /// Identifies the property that holds the event id.
    /// </summary>
    public const string EventIdProperty = "EventId";

    private static readonly MessageTemplateTextFormatter MessageFormatter = new(
        "{Message:lj}",
        CultureInfo.InvariantCulture
    );

    /// <summary>
    /// Builds the stored row of an event.
    /// </summary>
    /// <param name="logEvent">Event to store.</param>
    /// <returns>The row, with every text cleaned and truncated.</returns>
    public static DiagnosticLogEntry ToEntry(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var occurredAt = logEvent.Timestamp.ToUniversalTime();
        return new DiagnosticLogEntry
        {
            Id = Guid.CreateVersion7(occurredAt),
            OccurredAt = occurredAt,
            Level = (short)logEvent.Level,
            Category = LogText.Clean(
                Scalar(logEvent.Properties, CategoryProperty) ?? string.Empty,
                DiagnosticLogEntry.NameMaxLength
            ),
            EventName = LogText.CleanOrNull(EventName(logEvent), DiagnosticLogEntry.NameMaxLength),
            Message = LogText.Clean(Render(logEvent), DiagnosticLogEntry.MessageMaxLength),
            Exception = LogText.CleanOrNull(
                logEvent.Exception?.ToString(),
                DiagnosticLogEntry.ExceptionMaxLength
            ),
            TraceId = logEvent.TraceId?.ToHexString(),
            Properties = LogJson.FromProperties(TemplateProperties(logEvent)),
        };
    }

    private static string Render(LogEvent logEvent)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        MessageFormatter.Format(logEvent, output);
        return output.ToString();
    }

    private static List<LogEventProperty> TemplateProperties(LogEvent logEvent)
    {
        return
        [
            .. logEvent
                .MessageTemplate.Tokens.OfType<PropertyToken>()
                .Select(token => token.PropertyName)
                .Distinct(StringComparer.Ordinal)
                .Where(logEvent.Properties.ContainsKey)
                .Select(name => new LogEventProperty(name, logEvent.Properties[name])),
        ];
    }

    private static string? EventName(LogEvent logEvent)
    {
        return logEvent.Properties.TryGetValue(EventIdProperty, out var eventId)
            && eventId is StructureValue structure
            ? structure
                .Properties.Where(property => property.Name == "Name")
                .Select(property => (property.Value as ScalarValue)?.Value as string)
                .FirstOrDefault()
            : null;
    }

    private static string? Scalar(
        IReadOnlyDictionary<string, LogEventPropertyValue> properties,
        string name
    )
    {
        return properties.TryGetValue(name, out var value) && value is ScalarValue scalar
            ? scalar.Value as string
            : null;
    }
}
