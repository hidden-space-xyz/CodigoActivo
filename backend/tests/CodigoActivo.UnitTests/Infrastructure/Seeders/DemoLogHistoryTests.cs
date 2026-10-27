using System.Net;
using AwesomeAssertions;
using CodigoActivo.Domain.Activities;
using CodigoActivo.Domain.Users;
using CodigoActivo.Infrastructure.Database.Seeders;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Seeders;

public sealed class DemoLogHistoryTests
{
    private const int MaxFailedLogins = 4;
    private const int MaxFailedCodes = 3;

    private static readonly DateTimeOffset Now = new(2026, 7, 21, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid[] Adults =
    [
        .. Enumerable
            .Range(1, 6)
            .Select(index => new Guid($"dede0001-0000-0000-0000-00000000000{index}")),
    ];

    private static readonly Guid Child = new("dede0001-0000-0000-0000-000000000020");
    private static readonly Guid FeaturedEvent = new("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid EventCover = new("f0000000-0000-0000-0000-000000000001");
    private static readonly Guid Workshop = new("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkshopImage = new("f0000000-0000-0000-0000-000000000002");
    private static readonly Guid OldNewsItem = new("b0000000-0000-0000-0000-000000000001");

    private static readonly IPNetwork[] DocumentationNetworks =
    [
        IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("2001:db8::/32"),
    ];

    private readonly TestClock clock = new(Now, DateOnly.FromDateTime(Now.UtcDateTime));

    private readonly DemoLogSignup adultSignup = new(
        Workshop,
        FeaturedEvent,
        Adults[2],
        AssignmentStatus.Confirmed,
        Now.AddDays(-4)
    );

    private readonly DemoLogSignup childSignup = new(
        Workshop,
        FeaturedEvent,
        Child,
        AssignmentStatus.Requested,
        Now.AddDays(-3)
    );

    private DemoLogFacts Facts =>
        new(
            [
                new(Adults[0], null, UserType.Member, Now.AddDays(-2)),
                new(Adults[1], null, UserType.Sponsor, null),
                new(Adults[2], null, UserType.Member, Now.AddDays(-5)),
                new(Adults[3], null, UserType.Member, null),
                new(Adults[4], null, UserType.Member, Now.AddDays(-40)),
                new(Adults[5], null, UserType.Member, null),
                new(Child, Adults[0], UserType.Member, null),
            ],
            [
                adultSignup,
                childSignup,
                new(
                    Workshop,
                    FeaturedEvent,
                    Adults[3],
                    AssignmentStatus.Confirmed,
                    Now.AddDays(-40)
                ),
            ],
            [new(FeaturedEvent, EventCover, true, Adults[0], Now.AddDays(-10))],
            [
                new(
                    Workshop,
                    FeaturedEvent,
                    WorkshopImage,
                    Adults[0],
                    Now.AddDays(-10).AddMinutes(20)
                ),
            ],
            [new(OldNewsItem, EventCover, Adults[0], Now.AddDays(-30))]
        );

    private DemoLogBatch Build(DemoLogFacts? facts = null)
    {
        return DemoLogHistory.Build(facts ?? Facts, clock, MaxFailedLogins, MaxFailedCodes);
    }

    private List<AuditLogEntry> Audit()
    {
        return [.. Build().Audit.Select(AuditLogEvents.ToEntry)];
    }

    private List<DiagnosticLogEntry> Diagnostics()
    {
        return [.. Build().Diagnostics.Select(DiagnosticLogEvents.ToEntry)];
    }

    private static string Related(params (string Type, Guid Id)[] entities)
    {
        return "["
            + string.Join(
                ",",
                entities.Select(entity => $$"""{"type":"{{entity.Type}}","id":"{{entity.Id}}"}""")
            )
            + "]";
    }

    [Fact]
    public void BuildEveryEntryFallsInsideTheLastThreeWeeksOldestFirst()
    {
        var batch = Build();

        foreach (var events in new[] { batch.Audit, batch.Diagnostics })
        {
            events.Should().NotBeEmpty();
            events.Should().BeInAscendingOrder(logEvent => logEvent.Timestamp);
            events
                .Should()
                .OnlyContain(logEvent =>
                    logEvent.Timestamp > Now - DemoLogHistory.Span && logEvent.Timestamp < Now
                );
        }
    }

    [Fact]
    public void BuildSignupIsAuditedAtItsInstantByTheAccountAfterItSignsIn()
    {
        var audit = Audit();

        var signup = audit
            .Should()
            .ContainSingle(entry =>
                entry.Action == "AssignActivity"
                && entry.OccurredAt == adultSignup.CreatedAt
                && entry.Outcome == "Succeeded"
            )
            .Subject;
        signup.ActorType.Should().Be("User");
        signup.ActorId.Should().Be(Adults[2]);
        signup.EntityType.Should().Be("Activity");
        signup.EntityId.Should().Be(Workshop);
        signup.Related.Should().Be(Related(("User", Adults[2])));
        audit
            .Should()
            .Contain(entry =>
                entry.Action == "CompleteLogin"
                && entry.Outcome == "Succeeded"
                && entry.EntityId == Adults[2]
                && entry.OccurredAt < signup.OccurredAt
                && entry.OccurredAt > signup.OccurredAt.AddHours(-2)
            );
    }

    [Fact]
    public void BuildChildSignupIsRunByTheGuardian()
    {
        var signup = Audit()
            .Should()
            .ContainSingle(entry =>
                entry.Action == "AssignActivity" && entry.OccurredAt == childSignup.CreatedAt
            )
            .Subject;

        signup.ActorId.Should().Be(Adults[0]);
        signup.Related.Should().Be(Related(("User", Child)));
    }

    [Fact]
    public void BuildAdministratorDecidesSignupsOnALaterMorning()
    {
        var decisions = Audit().Where(entry => entry.Action == "ChangeAssignmentStatus").ToList();

        var decision = decisions.Should().ContainSingle().Subject;
        decision.ActorId.Should().Be(InitialAdministrator.Id.Value);
        decision.OccurredAt.Should().BeAfter(adultSignup.CreatedAt);
        decision.EntityId.Should().Be(Workshop);
        decision.Related.Should().Be(Related(("User", Adults[2])));
    }

    [Fact]
    public void BuildContentPublishedInsideTheSpanIsAuditedWithItsUpload()
    {
        var audit = Audit();

        var created = audit.Should().ContainSingle(entry => entry.Action == "CreateEvent").Subject;
        created.ActorId.Should().Be(Adults[0]);
        created.EntityId.Should().Be(FeaturedEvent);
        created.Related.Should().Be(Related(("StoredFile", EventCover)));
        audit
            .Should()
            .Contain(entry =>
                entry.Action == "CreateFile"
                && entry.EntityId == EventCover
                && entry.OccurredAt < created.OccurredAt
            );
        audit
            .Should()
            .ContainSingle(entry => entry.Action == "CreateActivity")
            .Which.Related.Should()
            .Be(Related(("Event", FeaturedEvent), ("StoredFile", WorkshopImage)));
        audit.Should().NotContain(entry => entry.Action == "CreateNewsItem");
    }

    [Fact]
    public void BuildPasswordAttackLocksTheAccountInTheTraceOfItsLastAttempt()
    {
        var batch = Build();
        var attempts = batch
            .Audit.Select(AuditLogEvents.ToEntry)
            .Where(entry =>
                entry.Action == "Login"
                && entry.EntityId == Adults[3]
                && Equals(entry.ClientIp, IPAddress.Parse("192.0.2.66"))
            )
            .ToList();

        attempts.Should().HaveCount(MaxFailedLogins);
        attempts.Should().OnlyContain(entry => entry.ErrorCode == "InvalidCredentials");
        var lockout = batch
            .Diagnostics.Select(DiagnosticLogEvents.ToEntry)
            .Should()
            .ContainSingle(entry => entry.EventName == "PasswordLockoutTriggered")
            .Subject;
        lockout.TraceId.Should().Be(attempts[^1].TraceId);
        lockout.Category.Should().Be("CodigoActivo.Application.Accounts.AccountLockoutLog");
        lockout.Message.Should().Be("An account was locked after 4 wrong passwords");
    }

    [Fact]
    public void BuildDiagnosticsAreDeclaredEventsOfTheApplication()
    {
        var diagnostics = Diagnostics();

        diagnostics
            .Should()
            .OnlyContain(entry =>
                entry.Category.StartsWith("CodigoActivo.", StringComparison.Ordinal)
                && entry.EventName != null
                && entry.Level >= 3
            );
        diagnostics
            .Select(entry => entry.EventName)
            .Distinct()
            .Should()
            .BeEquivalentTo(
                "EmailDeliveryAttemptFailed",
                "EmailDeliveryPaused",
                "EmailsExpired",
                "ManualEmailQueueFailed",
                "SlowUseCase",
                "DisposableEmailDomainDownloadFailed",
                "PasswordLockoutTriggered",
                "TwoFactorLockoutTriggered"
            );
        diagnostics.Should().Contain(entry => entry.Level == 4 && entry.Exception != null);
    }

    [Fact]
    public void BuildAttendeeEmailFailureSharesItsTraceWithTheQueueError()
    {
        var batch = Build();
        var audit = batch.Audit.Select(AuditLogEvents.ToEntry).ToList();
        var diagnostics = batch.Diagnostics.Select(DiagnosticLogEvents.ToEntry).ToList();

        var failed = audit
            .Should()
            .ContainSingle(entry =>
                entry.Action == "SendEmailToEventAttendees" && entry.Outcome == "Rejected"
            )
            .Subject;
        var sent = audit
            .Should()
            .ContainSingle(entry =>
                entry.Action == "SendEmailToEventAttendees" && entry.Outcome == "Succeeded"
            )
            .Subject;

        failed.ErrorCode.Should().Be("EmailSendFailed");
        diagnostics
            .Should()
            .ContainSingle(entry => entry.EventName == "ManualEmailQueueFailed")
            .Which.TraceId.Should()
            .Be(failed.TraceId);
        diagnostics
            .Should()
            .ContainSingle(entry =>
                entry.Message.Contains("SendEmailToEventAttendeesCommand", StringComparison.Ordinal)
            )
            .Which.TraceId.Should()
            .Be(sent.TraceId);
    }

    [Fact]
    public void BuildScheduledCleanupsRunDailyWithoutTraceOrAddress()
    {
        var cleanups = Audit().Where(entry => entry.Action == "RemoveExpiredSessions").ToList();

        cleanups.Should().HaveCountGreaterThanOrEqualTo(DemoLogHistory.Span.Days);
        cleanups
            .Should()
            .OnlyContain(entry =>
                entry.ActorType == "System" && entry.ClientIp == null && entry.TraceId == null
            );
    }

    [Fact]
    public void BuildCallersComeFromAddressesReservedForDocumentation()
    {
        var addresses = Audit()
            .Where(entry => entry.ClientIp != null)
            .Select(entry => entry.ClientIp!)
            .ToList();

        addresses.Should().NotBeEmpty();
        addresses
            .Should()
            .OnlyContain(address =>
                DocumentationNetworks.Any(network => network.Contains(address))
            );
    }

    [Fact]
    public void BuildRequestsCarryATraceAndRejectionsACode()
    {
        var audit = Audit().Where(entry => entry.ActorType != "System").ToList();

        audit.Should().OnlyContain(entry => entry.TraceId != null);
        audit
            .Where(entry => entry.Outcome != "Succeeded")
            .Should()
            .NotBeEmpty()
            .And.OnlyContain(entry => entry.Outcome == "Rejected" && entry.ErrorCode != null);
    }

    [Fact]
    public void BuildADemoAccountActsOnTheLastDays()
    {
        Audit()
            .Should()
            .Contain(entry =>
                entry.ActorId != null
                && Adults.Contains(entry.ActorId.Value)
                && entry.OccurredAt > Now.AddDays(-2)
            );
    }

    [Fact]
    public void BuildWithoutDemoAdultsHasNoHistory()
    {
        var batch = Build(new DemoLogFacts([], [], [], [], []));

        batch.Audit.Should().BeEmpty();
        batch.Diagnostics.Should().BeEmpty();
    }
}
