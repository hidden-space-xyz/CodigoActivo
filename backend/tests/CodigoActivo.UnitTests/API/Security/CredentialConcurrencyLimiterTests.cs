using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class CredentialConcurrencyLimiterTests
{
    [Fact]
    public void CredentialRequestsShareOneGlobalConcurrencyPartition()
    {
        using var sut = CredentialConcurrencyLimiter.Create(1, 0);
        var firstContext = NewContext(SecurityPolicies.Credentials);
        var secondContext = NewContext(SecurityPolicies.Credentials);

        using var first = sut.AttemptAcquire(firstContext);
        using var rejected = sut.AttemptAcquire(secondContext);

        first.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void OtherEndpointsAreNotConcurrencyLimited()
    {
        using var sut = CredentialConcurrencyLimiter.Create(1, 0);
        var credentialContext = NewContext(SecurityPolicies.Credentials);
        var otherContext = NewContext("other");

        using var credential = sut.AttemptAcquire(credentialContext);
        using var other = sut.AttemptAcquire(otherContext);

        credential.IsAcquired.Should().BeTrue();
        other.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task QueueAcceptsOneHundredConcurrentCredentialRequests()
    {
        using var sut = CredentialConcurrencyLimiter.Create(4, 128);
        var requests = Enumerable
            .Range(0, 100)
            .Select(async _ =>
            {
                using var lease = await sut.AcquireAsync(NewContext(SecurityPolicies.Credentials));
                if (!lease.IsAcquired)
                {
                    return false;
                }

                await Task.Delay(1);
                return true;
            });

        var results = await Task.WhenAll(requests);

        results.Should().OnlyContain(acquired => acquired);
    }

    private static DefaultHttpContext NewContext(string policyName)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(
            new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(new EnableRateLimitingAttribute(policyName)),
                "test"
            )
        );
        return context;
    }
}
