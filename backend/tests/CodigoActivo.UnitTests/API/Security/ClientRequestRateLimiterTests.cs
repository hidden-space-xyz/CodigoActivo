using System.Net;
using System.Security.Claims;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
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
    public void OneThousandAuthenticatedUsersDoNotShareAnIpBudget()
    {
        using var sut = ClientRequestRateLimiter.CreateGlobal(300, 3_000);

        var allAccepted = Enumerable
            .Range(0, 1_000)
            .All(_ =>
            {
                using var lease = sut.AttemptAcquire(
                    NewContext(IPAddress.Loopback, Guid.NewGuid())
                );
                return lease.IsAcquired;
            });

        allAccepted.Should().BeTrue();
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

    private static DefaultHttpContext NewContext(IPAddress address, Guid? userId = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;
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
