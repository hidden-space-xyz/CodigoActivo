using System.Diagnostics;
using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Logs;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Logs;

public sealed class DiagnosticLogEventsTests
{
    private const string Category = "CodigoActivo.Infrastructure.Communication.EmailOutboxDeliverer";

    private static LogEvent Capture(Action<Microsoft.Extensions.Logging.ILogger> write)
    {
        var sink = new CollectingSink();
        using var provider = new SerilogLoggerProvider(
            new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger(),
            dispose: true
        );

        write(provider.CreateLogger(Category));

        return sink.Events.Should().ContainSingle().Subject;
    }

    private static InvalidOperationException Failure()
    {
        try
        {
            throw new InvalidOperationException("boom\0");
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }

    [Fact]
    public void ToEntryKeepsTheDeclaredEventAndItsTemplateValuesOnly()
    {
        var logEvent = Capture(logger =>
        {
            using var scope = logger.BeginScope(
                new Dictionary<string, object> { ["RequestPath"] = "/api/users/ana@test.com" }
            );
            logger.EmailDeliveryAttemptFailed(3, EmailKind.PasswordReset, Failure());
        });

        var entry = DiagnosticLogEvents.ToEntry(logEvent);

        entry.Level.Should().Be(3);
        entry.Category.Should().Be(Category);
        entry.EventName.Should().Be(nameof(InfrastructureLog.EmailDeliveryAttemptFailed));
        entry
            .Message.Should()
            .Be("Attempt 3 to deliver a \"PasswordReset\" email failed and will be retried");
        entry.Properties.Should().Be("""{"Attempts":3,"Kind":"PasswordReset"}""");
        entry.Exception.Should().StartWith("System.InvalidOperationException: boom");
        entry.Exception.Should().NotContain("\0");
        entry.OccurredAt.Offset.Should().Be(TimeSpan.Zero);
        entry.Id.Version.Should().Be(7);
        entry.TraceId.Should().BeNull();
    }

    [Fact]
    public void ToEntryKeepsTheTraceOfTheCurrentActivity()
    {
        using var activity = new Activity("request").Start();

        var logEvent = Capture(logger => logger.EmailOutboxFull(1000));

        DiagnosticLogEvents.ToEntry(logEvent).TraceId.Should().Be(activity.TraceId.ToHexString());
    }

    [Fact]
    public void ToEntryTruncatesTheMessageToItsColumn()
    {
        var text = new string('x', DiagnosticLogEntry.MessageMaxLength + 50);
        var logEvent = new LogEvent(
            DateTimeOffset.UnixEpoch,
            LogEventLevel.Error,
            exception: null,
            new Serilog.Parsing.MessageTemplateParser().Parse("{Text}"),
            [new LogEventProperty("Text", new ScalarValue(text))]
        );

        var entry = DiagnosticLogEvents.ToEntry(logEvent);

        entry.Message.Should().HaveLength(DiagnosticLogEntry.MessageMaxLength);
        entry.Category.Should().BeEmpty();
        entry.EventName.Should().BeNull();
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
        }
    }
}
