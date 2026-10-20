using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Accounts.Contracts;
using CodigoActivo.API.Contracts;
using CodigoActivo.API.Participation.Contracts;
using CodigoActivo.API.Users.Contracts;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Domain.Users;
using CodigoActivo.Domain.Users.Erasure;
using CodigoActivo.Infrastructure.Users;
using CodigoActivo.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.IntegrationTests.Diagnostics;

public sealed class AuditTrailTests(CodigoActivoWebAppFactory factory)
    : IntegrationTestBase(factory)
{
    private const string ForwardedAddress = "203.0.113.9";

    private static readonly AuditEntity Member = new(
        AuditEntityType.User,
        TestSeedData.Users.MemberId
    );

    private HttpClient ClientBehindTheProxy()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ForwardedAddress);
        return client;
    }

    [Fact]
    public async Task LoginRecordsTheAccountAndTheCallerAddressWhetherItSucceedsOrNot()
    {
        using var client = ClientBehindTheProxy();

        await PassPasswordStepAsync(client, TestSeedData.MemberCredentials);
        using var refused = await client.PostJsonAsync(
            "/api/auth/login",
            new LoginRequest(TestSeedData.MemberEmail, "Wr0ngPass!23", false),
            Ct
        );

        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var logins = Factory
            .Audit.Records.Where(record => record.Action == AuditAction.Login)
            .ToList();
        logins.Should().HaveCount(2);
        logins
            .Should()
            .OnlyContain(record =>
                record.ActorType == AuditActorType.Anonymous
                && record.Entity == Member
                && Equals(record.ClientAddress, IPAddress.Parse(ForwardedAddress))
            );
        logins[0].Outcome.Should().Be(AuditOutcome.Succeeded);
        logins[1].Outcome.Should().Be(AuditOutcome.Rejected);
        logins[1].ErrorCode.Should().Be("InvalidCredentials");
    }

    [Fact]
    public async Task AdministratorChangeRecordsTheActorTheTargetAndTheFlag()
    {
        var client = await LoginAsAdminAsync();

        using var response = await client.PatchJsonAsync(
            $"/api/users/{TestSeedData.Users.MemberId}/admin",
            new SetAdminRequest(true, TestSeedData.Password),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var record = Factory
            .Audit.Records.Should()
            .ContainSingle(record => record.Action == AuditAction.SetAdmin)
            .Subject;
        record.ActorType.Should().Be(AuditActorType.User);
        record.ActorId.Should().Be(UserId.From(TestSeedData.Users.AdminId));
        record.Entity.Should().Be(Member);
        record.Outcome.Should().Be(AuditOutcome.Succeeded);
        record.ToString().Should().NotContain(TestSeedData.Password);
    }

    [Fact]
    public async Task EventRatingIsNeverRecorded()
    {
        var client = await LoginAsMemberAsync();
        var before = Factory.Audit.Records.Count;

        using var response = await client.PostJsonAsync(
            $"/api/events/{Guid.NewGuid()}/rating",
            new SaveEventRatingRequest(5, "Todo", null, null),
            Ct
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Factory.Audit.Records.Should().HaveCount(before);
    }

    [Fact]
    public async Task ScheduledPurgeIsRecordedAsTheSystemOnlyWhenItRemovedSomething()
    {
        var purger = new DeletedAccountPurgeWorker(
            Factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new DeletedAccountPurgeOptions(),
            NullLogger<DeletedAccountPurgeWorker>.Instance
        );

        await purger.PurgeAsync(Ct);
        Factory.Audit.Records.Should().BeEmpty();

        await Factory.SeedAsync(db =>
        {
            db.DeletedAccounts.Add(
                Persisted.As<DeletedAccount>(
                    new { Id = Guid.NewGuid(), DeletedAt = Factory.Clock.UtcNow.AddYears(-3) }
                )
            );
            return Task.CompletedTask;
        });
        await purger.PurgeAsync(Ct);

        var record = Factory.Audit.Records.Should().ContainSingle().Subject;
        record.Action.Should().Be(AuditAction.PurgeDeletedAccounts);
        record.ActorType.Should().Be(AuditActorType.System);
        record.ClientAddress.Should().BeNull();
    }

    [Fact]
    public async Task ErrorReferenceIsATraceTheClientCannotChoose()
    {
        using var client = CreateClient();
        const string ChosenTrace = "4bf92f3577b34da6a3ce929d0e0e4736";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/no-such-endpoint");
        request.Headers.Add("traceparent", $"00-{ChosenTrace}-00f067aa0ba902b7-01");

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.ReadJsonAsync<ApiErrorResponse>(Ct);
        body!.TraceId.Should().MatchRegex("^[0-9a-f]{32}$").And.NotBe(ChosenTrace);
    }
}
