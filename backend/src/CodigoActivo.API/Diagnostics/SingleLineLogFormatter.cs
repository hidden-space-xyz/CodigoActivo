using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Parsing;

namespace CodigoActivo.API.Diagnostics;

/// <summary>
/// Writes one line per event: UTC timestamp, level, category, rendered message and, when there is
/// one, the exception. Values are rendered literally, without the quotes Serilog adds to strings,
/// and line breaks inside the message or the exception are folded into a separator so a single entry
/// never spans several lines.
/// <para>
/// The message and the exception are also stripped of identifiers before they are written: every GUID
/// they contain, in the canonical form and in the 32 digit form the project uses for stored file
/// names, ETags and rate limiting keys, becomes the literal <c>&lt;id&gt;</c>. A template never names
/// an identifier, but an exception raised deeper down quotes the path or the key it failed on, and a
/// retained file must not end up holding a row identifier that points at a person. The timestamp, the
/// level and the category are written untouched, and the exception itself is still recorded whole, so
/// the diagnostic value of the entry survives.
/// </para>
/// </summary>
internal sealed partial class SingleLineLogFormatter : ITextFormatter
{
    private const string Separator = " | ";
    private const string NoCategory = "-";
    private const string IdentifierMask = "<id>";
    private static readonly string[] LineBreaks = ["\r\n", "\n", "\r"];

    [GeneratedRegex(
        @"(?<![0-9A-Fa-f])(?:[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}|[0-9A-Fa-f]{32})(?![0-9A-Fa-f])",
        RegexOptions.None,
        matchTimeoutMilliseconds: 250
    )]
    private static partial Regex IdentifierPattern();

    /// <summary>
    /// Formats the event as a single line.
    /// </summary>
    /// <param name="logEvent">Event to write.</param>
    /// <param name="output">Writer of the log file.</param>
    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        output.Write(
            logEvent.Timestamp.UtcDateTime.ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture
            )
        );
        output.Write(' ');
        output.Write(Abbreviate(logEvent.Level));
        output.Write(' ');
        output.Write(Category(logEvent));
        output.Write(' ');
        output.Write(Sanitize(RenderMessage(logEvent)));

        if (logEvent.Exception is not null)
        {
            output.Write(Separator);
            output.Write(Sanitize(logEvent.Exception.ToString()));
        }

        output.Write('\n');
    }

    private static string RenderMessage(LogEvent logEvent)
    {
        var message = new StringBuilder();
        foreach (var token in logEvent.MessageTemplate.Tokens)
        {
            message.Append(Render(token, logEvent));
        }

        return message.ToString();
    }

    private static string Render(MessageTemplateToken token, LogEvent logEvent)
    {
        if (token is not PropertyToken property)
        {
            return token.ToString() ?? string.Empty;
        }

        return logEvent.Properties.TryGetValue(property.PropertyName, out var value)
            ? Literal(value)
            : token.ToString() ?? string.Empty;
    }

    private static string Literal(LogEventPropertyValue value)
    {
        return value is ScalarValue scalar
            ? Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? string.Empty
            : value.ToString(null, CultureInfo.InvariantCulture);
    }

    private static string Category(LogEvent logEvent)
    {
        return
            logEvent.Properties.TryGetValue("SourceContext", out var value)
            && value is ScalarValue { Value: string category }
            ? category
            : NoCategory;
    }

    private static string Sanitize(string value)
    {
        return IdentifierPattern().Replace(Fold(value), IdentifierMask);
    }

    private static string Fold(string value)
    {
        foreach (var lineBreak in LineBreaks)
        {
            value = value.Replace(lineBreak, Separator, StringComparison.Ordinal);
        }

        return value;
    }

    private static string Abbreviate(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "TRC",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            _ => "CRT",
        };
    }
}
