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
        using var sut = EndpointConcurrencyLimiter.Create(SecurityPolicies.Credentials, 1, 0);
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
        using var sut = EndpointConcurrencyLimiter.Create(SecurityPolicies.Credentials, 1, 0);
        var credentialContext = NewContext(SecurityPolicies.Credentials);
        var otherContext = NewContext("other");

        using var credential = sut.AttemptAcquire(credentialContext);
        using var other = sut.AttemptAcquire(otherContext);

        credential.IsAcquired.Should().BeTrue();
        other.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task QueueAcceptsConfiguredNumberOfSelectedPolicyRequests()
    {
        const int PermitLimit = 4;
        const int QueueLimit = 16;
        using var sut = EndpointConcurrencyLimiter.Create(
            SecurityPolicies.Credentials,
            PermitLimit,
            QueueLimit
        );
        var requests = Enumerable
            .Range(0, PermitLimit + QueueLimit)
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

    [Fact]
    public void TenFileUploadsCanExecuteConcurrently()
    {
        using var sut = EndpointConcurrencyLimiter.Create(SecurityPolicies.FileUploads, 12, 12);
        var leases = Enumerable
            .Range(0, 10)
            .Select(_ => sut.AttemptAcquire(NewContext(SecurityPolicies.FileUploads)))
            .ToArray();

        try
        {
            leases.Should().OnlyContain(lease => lease.IsAcquired);
        }
        finally
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }
        }
    }

    [Fact]
    public void TenSingleRecipientEmailsCanExecuteConcurrently()
    {
        using var sut = EndpointConcurrencyLimiter.Create(
            SecurityPolicies.SingleRecipientEmail,
            10,
            10
        );
        var leases = Enumerable
            .Range(0, 10)
            .Select(_ => sut.AttemptAcquire(NewContext(SecurityPolicies.SingleRecipientEmail)))
            .ToArray();

        try
        {
            leases.Should().OnlyContain(lease => lease.IsAcquired);
        }
        finally
        {
            foreach (var lease in leases)
            {
                lease.Dispose();
            }
        }
    }

    [Fact]
    public void ThirdBulkEmailIsRejectedWithoutWaiting()
    {
        using var sut = EndpointConcurrencyLimiter.Create(SecurityPolicies.BulkEmail, 2, 0);

        using var first = sut.AttemptAcquire(NewContext(SecurityPolicies.BulkEmail));
        using var second = sut.AttemptAcquire(NewContext(SecurityPolicies.BulkEmail));
        using var rejected = sut.AttemptAcquire(NewContext(SecurityPolicies.BulkEmail));

        first.IsAcquired.Should().BeTrue();
        second.IsAcquired.Should().BeTrue();
        rejected.IsAcquired.Should().BeFalse();
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
