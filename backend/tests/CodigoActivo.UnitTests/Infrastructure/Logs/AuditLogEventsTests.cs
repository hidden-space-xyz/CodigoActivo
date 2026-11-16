using AwesomeAssertions;
using CodigoActivo.Infrastructure.Logs;
using Serilog.Events;
using Serilog.Parsing;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Logs;

public sealed class AuditLogEventsTests
{
    private static LogEvent Event(params LogEventProperty[] properties)
    {
        return new LogEvent(
            new DateTimeOffset(2026, 7, 4, 12, 0, 0, TimeSpan.Zero),
            LogEventLevel.Information,
            exception: null,
            new MessageTemplateParser().Parse("{Record}"),
            properties
        );
    }

    [Fact]
    public void ToEntryEventWithoutAnAuditRecordIsRefused()
    {
        LogEvent[] events =
        [
            Event(),
            Event(
                new LogEventProperty(
                    AuditLogEvents.RecordProperty,
                    new StructureValue([new LogEventProperty("Action", new ScalarValue("Login"))])
                )
            ),
            Event(new LogEventProperty(AuditLogEvents.RecordProperty, new ScalarValue("Login"))),
        ];

        foreach (var logEvent in events)
        {
            var act = () => AuditLogEvents.ToEntry(logEvent);

            act.Should().Throw<ArgumentException>().WithParameterName("logEvent");
        }
    }
}
