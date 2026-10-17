using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Errors;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Logs.Contracts;
using CodigoActivo.Infrastructure.Logs;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodigoActivo.IntegrationTests.Controllers;

public sealed class LogsControllerTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string Trace = "4bf92f3577b34da6a3ce929d0e0e4736";

    private static readonly DateTimeOffset Morning = new(2026, 7, 4, 9, 0, 0, TimeSpan.Zero);

    private static readonly Guid ErasedAccount = new("99999999-9999-9999-9999-999999999999");
    private static readonly Guid Activity = new("aaaaaaaa-1111-1111-1111-111111111111");

    private static DiagnosticLogEntry Diagnostic(
        DateTimeOffset occurredAt,
        short level,
        string message,
        string category = "CodigoActivo.Tests",
        string? eventName = null,
        string? exception = null,
        string? traceId = null
    )
    {
        return new DiagnosticLogEntry
        {
            Id = Guid.CreateVersion7(occurredAt),
            OccurredAt = occurredAt,
            Level = level,
            Category = category,
            EventName = eventName,
            Message = message,
            Exception = exception,
            TraceId = traceId,
            Properties = exception is null ? null : """{"Method": "GET"}""",
        };
    }

    private static AuditLogEntry Audit(
        DateTimeOffset occurredAt,
        AuditAction action,
        Guid? actor,
        Guid? entity,
        string outcome = "Succeeded",
        string? related = null,
        string? clientIp = null,
        string? traceId = null
    )
    {
        return new AuditLogEntry
        {
            Id = Guid.CreateVersion7(occurredAt),
            OccurredAt = occurredAt,
            ActorType = actor is null ? "Anonymous" : "User",
            ActorId = actor,
            ClientIp = clientIp is null ? null : IPAddress.Parse(clientIp),
            Action = action.ToString(),
            EntityType = entity is null ? null : "User",
            EntityId = entity,
            Related = related,
            Details = action is AuditAction.SetAdmin ? """{"IsAdmin": true}""" : null,
            Outcome = outcome,
            ErrorCode = outcome == "Rejected" ? "InvalidCredentials" : null,
            TraceId = traceId,
        };
    }

    private async Task SeedAsync(params object[] entries)
    {
        await using (
            var connection = await Factory
                .Services.GetRequiredService<LogDatabase>()
                .OpenConnectionAsync(Ct)
        )
        {
            await Factory
                .Services.GetRequiredService<LogPartitionMaintainer>()
                .EnsurePartitionsAsync(
                    connection,
                    LogTable.Diagnostics,
                    [new DateOnly(2026, 7, 3)],
                    Ct
                );
        }

        await Factory.SeedAsync(db =>
        {
            db.AddRange(entries);
            return Task.CompletedTask;
        });
    }

    private static async Task<PagedResult<T>> GetPageAsync<T>(HttpClient client, string url)
    {
        using var response = await client.GetAsync(TestUri.Rel(url), Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, url);
        return (await response.ReadJsonAsync<PagedResult<T>>(Ct))!;
    }

    [Theory]
    [InlineData("/api/logs/diagnostics")]
    [InlineData("/api/logs/audit")]
    [InlineData("/api/logs/diagnostics/0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b")]
    public async Task EndpointsAreOnlyForAdministrators(string url)
    {
        using var anonymous = await CreateClient().GetAsync(TestUri.Rel(url), Ct);
        var member = await LoginAsMemberAsync();
        using var refused = await member.GetAsync(TestUri.Rel(url), Ct);

        await anonymous.ShouldBeUnauthorizedAsync(ErrorCode.AuthenticationRequired);
        await refused.ShouldBeForbiddenAsync(ErrorCode.AccessDenied);
    }

    [Fact]
    public async Task ListDiagnosticsFiltersAndSortsNewestFirst()
    {
        var failure = Diagnostic(
            Morning.AddMinutes(2),
            4,
            "Unhandled exception while processing GET api/users/{id}",
            category: "CodigoActivo.API.Middlewares.GlobalExceptionHandler",
            eventName: "UnhandledRequestException",
            exception: "System.InvalidOperationException: boom",
            traceId: Trace
        );
        await SeedAsync(
            Diagnostic(Morning, 3, "GetUserQuery took 5432 ms"),
            Diagnostic(
                Morning.AddMinutes(1),
                3,
                "The disposable email domain list could not be downloaded"
            ),
            failure,
            Diagnostic(Morning.AddDays(-1), 4, "Yesterday's failure")
        );
        var admin = await LoginAsAdminAsync();

        var all = await GetPageAsync<DiagnosticLogEntryResponse>(admin, "/api/logs/diagnostics");
        var byLevel = await GetPageAsync<DiagnosticLogEntryResponse>(
            admin,
            "/api/logs/diagnostics?level=Error&occurredFrom=2026-07-04"
        );
        var byText = await GetPageAsync<DiagnosticLogEntryResponse>(
            admin,
            "/api/logs/diagnostics?search=DISPOSABLE%20email"
        );
        var bySource = await GetPageAsync<DiagnosticLogEntryResponse>(
            admin,
            "/api/logs/diagnostics?category=unhandledrequest"
        );
        var byTrace = await GetPageAsync<DiagnosticLogEntryResponse>(
            admin,
            $"/api/logs/diagnostics?traceId={Trace.ToUpperInvariant()}"
        );
        var oldestFirst = await GetPageAsync<DiagnosticLogEntryResponse>(
            admin,
            "/api/logs/diagnostics?sort=occurredAt&pageSize=2&page=2"
        );

        all.Total.Should().Be(4);
        all.Items[0]
            .Should()
            .Be(
                new DiagnosticLogEntryResponse(
                    failure.Id,
                    failure.OccurredAt,
                    DiagnosticLevel.Error,
                    failure.Category,
                    failure.EventName,
                    failure.Message,
                    HasException: true,
                    Trace
                )
            );
        all.Items.Select(entry => entry.OccurredAt).Should().BeInDescendingOrder();
        byLevel.Items.Should().ContainSingle().Which.Id.Should().Be(failure.Id);
        byText
            .Items.Should()
            .ContainSingle()
            .Which.Message.Should()
            .Be("The disposable email domain list could not be downloaded");
        bySource.Items.Should().ContainSingle().Which.Id.Should().Be(failure.Id);
        byTrace.Items.Should().ContainSingle().Which.Id.Should().Be(failure.Id);
        oldestFirst.Total.Should().Be(4);
        oldestFirst
            .Items.Select(entry => entry.Message)
            .Should()
            .Equal("The disposable email domain list could not be downloaded", failure.Message);
    }

    [Fact]
    public async Task GetDiagnosticReturnsTheExceptionAndTheTemplateValues()
    {
        var failure = Diagnostic(
            Morning,
            4,
            "Unhandled exception",
            exception: "System.InvalidOperationException: boom\n   at Somewhere()"
        );
        await SeedAsync(failure);
        var admin = await LoginAsAdminAsync();

        using var found = await admin.GetAsync(
            TestUri.Rel($"/api/logs/diagnostics/{failure.Id}"),
            Ct
        );
        using var missing = await admin.GetAsync(
            TestUri.Rel($"/api/logs/diagnostics/{Guid.CreateVersion7(Morning)}"),
            Ct
        );
        using var notVersion7 = await admin.GetAsync(
            TestUri.Rel($"/api/logs/diagnostics/{Guid.NewGuid()}"),
            Ct
        );

        found.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await found.ReadJsonAsync<DiagnosticLogEntryDetailResponse>(Ct);
        detail!.Exception.Should().Be(failure.Exception);
        detail.Properties.Should().Be("""{"Method": "GET"}""");
        await missing.ShouldBeNotFoundAsync(ErrorCode.DiagnosticLogEntryNotFound);
        await notVersion7.ShouldBeNotFoundAsync(ErrorCode.DiagnosticLogEntryNotFound);
    }

    [Fact]
    public async Task ListAuditJoinsTheUsersItMentionsAndKeepsErasedAccountsAnonymous()
    {
        var setAdmin = Audit(
            Morning.AddMinutes(1),
            AuditAction.SetAdmin,
            TestSeedData.Users.AdminId,
            TestSeedData.Users.MemberId,
            related: $$"""[{"type": "Activity", "id": "{{Activity}}"}]""",
            clientIp: "203.0.113.9",
            traceId: Trace
        );
        var erased = Audit(Morning, AuditAction.ChangePassword, ErasedAccount, ErasedAccount);
        await SeedAsync(setAdmin, erased);
        var admin = await LoginAsAdminAsync();

        var page = await GetPageAsync<AuditLogEntryResponse>(
            admin,
            "/api/logs/audit?actorType=User"
        );

        page.Items.Should().HaveCount(2);
        page.Items[0]
            .Should()
            .BeEquivalentTo(
                new AuditLogEntryResponse(
                    setAdmin.Id,
                    setAdmin.OccurredAt,
                    AuditActorType.User,
                    TestSeedData.Users.AdminId,
                    "Ada Admin",
                    TestSeedData.AdminEmail,
                    "203.0.113.9",
                    AuditAction.SetAdmin,
                    AuditEntityType.User,
                    TestSeedData.Users.MemberId,
                    "Marta Miembro",
                    [new AuditRelatedEntityResponse(AuditEntityType.Activity, Activity)],
                    [new AuditDetailResponse("IsAdmin", "true")],
                    AuditOutcome.Succeeded,
                    ErrorCode: null,
                    ItemCount: null,
                    Trace
                )
            );
        page.Items[1].ActorName.Should().BeNull();
        page.Items[1].ActorEmail.Should().BeNull();
        page.Items[1].EntityName.Should().BeNull();
    }

    [Theory]
    [InlineData("search=marta", 2)]
    [InlineData("search=ADMIN%40codigoactivo", 1)]
    [InlineData("actorType=Anonymous", 1)]
    [InlineData("actorId=99999999-9999-9999-9999-999999999999", 0)]
    [InlineData("action=Login", 1)]
    [InlineData("entityType=User", 2)]
    [InlineData("entityId=aaaaaaaa-1111-1111-1111-111111111111", 1)]
    [InlineData("outcome=Rejected", 1)]
    [InlineData("clientIp=203.0.113.9", 1)]
    [InlineData("traceId=4bf92f3577b34da6a3ce929d0e0e4736", 1)]
    [InlineData("occurredTo=2026-07-03", 0)]
    [InlineData("sort=action", 2)]
    public async Task ListAuditFiltersOnEveryCriterion(string filter, int expected)
    {
        await SeedAsync(
            Audit(
                Morning.AddMinutes(1),
                AuditAction.SetAdmin,
                TestSeedData.Users.AdminId,
                TestSeedData.Users.MemberId,
                related: $$"""[{"type": "Activity", "id": "{{Activity}}"}]""",
                clientIp: "203.0.113.9",
                traceId: Trace
            ),
            Audit(
                Morning,
                AuditAction.Login,
                null,
                TestSeedData.Users.MemberId,
                outcome: "Rejected"
            )
        );
        var admin = await LoginAsAdminAsync();

        var page = await GetPageAsync<AuditLogEntryResponse>(admin, $"/api/logs/audit?{filter}");

        page.Total.Should().Be(expected, filter);
    }

    [Fact]
    public async Task ListAuditSortedByActionUsesItsName()
    {
        await SeedAsync(
            Audit(Morning, AuditAction.SetAdmin, TestSeedData.Users.AdminId, null),
            Audit(Morning.AddMinutes(1), AuditAction.Login, null, null)
        );
        var admin = await LoginAsAdminAsync();

        var page = await GetPageAsync<AuditLogEntryResponse>(admin, "/api/logs/audit?sort=action");

        page.Items.Select(entry => entry.Action)
            .Should()
            .Equal(AuditAction.Login, AuditAction.SetAdmin);
    }

    [Theory]
    [InlineData("/api/logs/audit?clientIp=203.0.113")]
    [InlineData("/api/logs/audit?traceId=not-a-trace")]
    [InlineData("/api/logs/diagnostics?level=Loud")]
    public async Task InvalidCriteriaAreRefused(string url)
    {
        var admin = await LoginAsAdminAsync();

        using var response = await admin.GetAsync(TestUri.Rel(url), Ct);

        await response.ShouldBeBadRequestAsync(ErrorCode.RequestValidationFailed);
    }
}
