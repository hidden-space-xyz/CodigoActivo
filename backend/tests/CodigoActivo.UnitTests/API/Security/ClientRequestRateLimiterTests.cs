using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class ClientRequestRateLimiterTests
{
    [Fact]
    public void AuthenticatedUsersHaveIndependentBudgetsBehindOneIp()
    {
        using var sut = ClientRequestRateLimiter.CreateGlobal(1, 1);
        var firstUser = NewContext(IPAddress.Loopback, Guid.NewGuid());
        var secondUser = NewContext(IPAddress.Loopback, Guid.NewGuid());

        using var first = sut.AttemptAcquire(firstUser);
        using var firstRejected = sut.AttemptAcquire(firstUser);
        using var second = sut.AttemptAcquire(secondUser);

        first.IsAcquired.Should().BeTrue();
        firstRejected.IsAcquired.Should().BeFalse();
        second.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void AnonymousRequestsShareTheBudgetForTheirIp()
    {
        using var sut = ClientRequestRateLimiter.CreateGlobal(10, 1);
        var first = NewContext(IPAddress.Loopback);
        var sameIp = NewContext(IPAddress.Loopback);
        var otherIp = NewContext(IPAddress.Parse("192.0.2.10"));

        using var accepted = sut.AttemptAcquire(first);
        using var rejected = sut.AttemptAcquire(sameIp);
        using var independent = sut.AttemptAcquire(otherIp);

        accepted.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
        independent.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void IpPolicySharesTheBudgetOfAnIpAcrossUsers()
    {
        using var sut = ClientRequestRateLimiter.CreateIpPolicy(SecurityPolicies.Credentials, 1);
        var firstUser = NewContext(
            IPAddress.Loopback,
            Guid.NewGuid(),
            SecurityPolicies.Credentials
        );
        var secondUser = NewContext(
            IPAddress.Loopback,
            Guid.NewGuid(),
            SecurityPolicies.Credentials
        );
        var otherIp = NewContext(
            IPAddress.Parse("192.0.2.10"),
            Guid.NewGuid(),
            SecurityPolicies.Credentials
        );

        using var accepted = sut.AttemptAcquire(firstUser);
        using var rejected = sut.AttemptAcquire(secondUser);
        using var independent = sut.AttemptAcquire(otherIp);

        accepted.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
        independent.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void AuthenticatedPolicyCountsOnlyTheEndpointsOfThePolicy()
    {
        using var sut = ClientRequestRateLimiter.CreateAuthenticatedPolicy(
            SecurityPolicies.Reports,
            1
        );
        var userId = Guid.NewGuid();

        using var report = sut.AttemptAcquire(
            NewContext(IPAddress.Loopback, userId, SecurityPolicies.Reports)
        );
        using var rejected = sut.AttemptAcquire(
            NewContext(IPAddress.Loopback, userId, SecurityPolicies.Reports)
        );
        using var otherPolicy = sut.AttemptAcquire(
            NewContext(IPAddress.Loopback, userId, SecurityPolicies.FileUploads)
        );
        using var withoutPolicy = sut.AttemptAcquire(NewContext(IPAddress.Loopback, userId));

        report.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
        otherPolicy.IsAcquired.Should().BeTrue();
        withoutPolicy.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void ApiConcurrencyIsBoundedAcrossClients()
    {
        using var sut = ClientRequestRateLimiter.CreateConcurrency(2, 0);

        using var first = sut.AttemptAcquire(NewContext(IPAddress.Loopback));
        using var second = sut.AttemptAcquire(NewContext(IPAddress.Parse("192.0.2.10")));
        using var rejected = sut.AttemptAcquire(NewContext(IPAddress.Parse("192.0.2.11")));

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
    }

    private static DefaultHttpContext NewContext(
        IPAddress address,
        Guid? userId = null,
        string? policyName = null
    )
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;
        if (policyName is not null)
        {
            context.SetEndpoint(
                new Endpoint(
                    _ => Task.CompletedTask,
                    new EndpointMetadataCollection(new EnableRateLimitingAttribute(policyName)),
                    "test"
                )
            );
        }

        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())],
                    "test"
                )
            );
        }

        return context;
    }
}
