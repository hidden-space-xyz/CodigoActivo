using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class EndpointConcurrencyLimiterTests
{
    [Fact]
    public void RequestsForSelectedPolicyShareOneGlobalConcurrencyPartition()
    {
        using var sut = EndpointConcurrencyLimiter.Create(SecurityPolicies.Reports, 1, 0);
        var firstContext = NewContext(new EnableRateLimitingAttribute(SecurityPolicies.Reports));
        var secondContext = NewContext(new EnableRateLimitingAttribute(SecurityPolicies.Reports));

        using var first = sut.AttemptAcquire(firstContext);
        using var rejected = sut.AttemptAcquire(secondContext);

        first.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void OtherEndpointsAreNotConcurrencyLimited()
    {
        using var sut = EndpointConcurrencyLimiter.Create(SecurityPolicies.Reports, 1, 0);
        var reportContext = NewContext(new EnableRateLimitingAttribute(SecurityPolicies.Reports));
        var otherContext = NewContext(new EnableRateLimitingAttribute("other"));

        using var report = sut.AttemptAcquire(reportContext);
        using var other = sut.AttemptAcquire(otherContext);

        report.IsAcquired.Should().BeTrue();
        other.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void CreateForMarkerMarkedEndpointsShareOnePartition()
    {
        using var sut = EndpointConcurrencyLimiter.CreateForMarker<PasswordHashingAttribute>(1, 0);
        var login = NewContext(
            new EnableRateLimitingAttribute(SecurityPolicies.Credentials),
            new PasswordHashingAttribute()
        );
        var register = NewContext(
            new EnableRateLimitingAttribute(SecurityPolicies.Credentials),
            new PasswordHashingAttribute()
        );

        using var first = sut.AttemptAcquire(login);
        using var rejected = sut.AttemptAcquire(register);

        first.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void CreateForMarkerUnmarkedCredentialEndpointsAreNotConcurrencyLimited()
    {
        using var sut = EndpointConcurrencyLimiter.CreateForMarker<PasswordHashingAttribute>(1, 0);
        var login = NewContext(
            new EnableRateLimitingAttribute(SecurityPolicies.Credentials),
            new PasswordHashingAttribute()
        );
        var secondFactor = NewContext(
            new EnableRateLimitingAttribute(SecurityPolicies.Credentials)
        );

        using var hashing = sut.AttemptAcquire(login);
        using var other = sut.AttemptAcquire(secondFactor);

        hashing.IsAcquired.Should().BeTrue();
        other.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task CreateForMarkerQueueAcceptsConfiguredNumberOfMarkedRequests()
    {
        const int PermitLimit = 4;
        const int QueueLimit = 64;
        using var sut = EndpointConcurrencyLimiter.CreateForMarker<PasswordHashingAttribute>(
            PermitLimit,
            QueueLimit
        );
        var requests = Enumerable
            .Range(0, PermitLimit + QueueLimit)
            .Select(async _ =>
            {
                using var lease = await sut.AcquireAsync(
                    NewContext(new PasswordHashingAttribute())
                );
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

    private static DefaultHttpContext NewContext(params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(
            new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test")
        );
        return context;
    }
}
