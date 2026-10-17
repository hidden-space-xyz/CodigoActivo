using AwesomeAssertions;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Logs;
using CodigoActivo.Application.Logs.Contracts;
using CodigoActivo.Application.Logs.Queries;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Application.Logs;

public sealed class LogQueryHandlerTests
{
    private static readonly TimeZoneInfo Madrid = TimeZoneInfo.FindSystemTimeZoneById(
        "Europe/Madrid"
    );

    private readonly FakeLogEntryReader reader = new();
    private readonly TestClock clock = new() { TimeZone = Madrid };

    [Fact]
    public async Task HandleAsyncDiagnosticListReadsTheDaysInTheLocalTimeZone()
    {
        var filters = new DiagnosticLogEntryListQuery
        {
            Level = DiagnosticLevel.Error,
            OccurredFrom = new DateOnly(2026, 7, 4),
            OccurredTo = new DateOnly(2026, 7, 5),
        };

        var result = await new ListDiagnosticLogEntriesQueryHandler(reader, clock).HandleAsync(
            new ListDiagnosticLogEntriesQuery(filters),
            TestContext.Current.CancellationToken
        );

        result.Should().BeSameAs(reader.Diagnostics);
        reader.Criteria.Should().BeSameAs(filters);
        reader
            .Period.Should()
            .Be(
                new LogPeriod(
                    new DateTimeOffset(2026, 7, 3, 22, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 7, 5, 22, 0, 0, TimeSpan.Zero)
                )
            );
    }

    [Fact]
    public async Task HandleAsyncAuditListWithoutDaysIsNotLimitedInTime()
    {
        var filters = new AuditLogEntryListQuery { ClientIp = "203.0.113.9" };

        var result = await new ListAuditLogEntriesQueryHandler(reader, clock).HandleAsync(
            new ListAuditLogEntriesQuery(filters),
            TestContext.Current.CancellationToken
        );

        result.Should().BeSameAs(reader.Audit);
        reader.Criteria.Should().BeSameAs(filters);
        reader.Period.Should().Be(new LogPeriod(null, null));
    }

    [Fact]
    public async Task HandleAsyncDiagnosticEntryFoundIsReturned()
    {
        var entry = new DiagnosticLogEntryDetailResponse(
            Guid.CreateVersion7(),
            DateTimeOffset.UnixEpoch,
            DiagnosticLevel.Error,
            "Tests",
            null,
            "boom",
            "System.Exception: boom",
            null,
            null
        );
        reader.Detail = entry;

        var result = await new GetDiagnosticLogEntryQueryHandler(reader).HandleAsync(
            new GetDiagnosticLogEntryQuery(entry.Id),
            TestContext.Current.CancellationToken
        );

        result.Value.Should().BeSameAs(entry);
        reader.Id.Should().Be(entry.Id);
    }

    [Fact]
    public async Task HandleAsyncDiagnosticEntryMissingIsNotFound()
    {
        var result = await new GetDiagnosticLogEntryQueryHandler(reader).HandleAsync(
            new GetDiagnosticLogEntryQuery(Guid.CreateVersion7()),
            TestContext.Current.CancellationToken
        );

        result.ShouldFail(ErrorKind.NotFound, ApplicationErrorCode.DiagnosticLogEntryNotFound);
    }

    private sealed class FakeLogEntryReader : ILogEntryReader
    {
        public PagedResult<DiagnosticLogEntryResponse> Diagnostics { get; } = new([], 0, 1, 25);

        public PagedResult<AuditLogEntryResponse> Audit { get; } = new([], 0, 1, 25);

        public DiagnosticLogEntryDetailResponse? Detail { get; set; }

        public object? Criteria { get; private set; }

        public LogPeriod? Period { get; private set; }

        public Guid? Id { get; private set; }

        public Task<PagedResult<DiagnosticLogEntryResponse>> ListDiagnosticsAsync(
            DiagnosticLogEntryListQuery criteria,
            LogPeriod period,
            CancellationToken ct = default
        )
        {
            Criteria = criteria;
            Period = period;
            return Task.FromResult(Diagnostics);
        }

        public Task<DiagnosticLogEntryDetailResponse?> FindDiagnosticAsync(
            Guid id,
            CancellationToken ct = default
        )
        {
            Id = id;
            return Task.FromResult(Detail);
        }

        public Task<PagedResult<AuditLogEntryResponse>> ListAuditAsync(
            AuditLogEntryListQuery criteria,
            LogPeriod period,
            CancellationToken ct = default
        )
        {
            Criteria = criteria;
            Period = period;
            return Task.FromResult(Audit);
        }
    }
}
