using System.Net;
using System.Reflection;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using CodigoActivo.API.Configuration;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace CodigoActivo.UnitTests.API.Configuration;

public sealed class ApiRateLimitConfigurationTests
{
    [Fact]
    public async Task GlobalLimiterRefusesRequestsOverTheirPolicyBudgetWithoutQueueing()
    {
        using var sut = ApiRateLimitConfiguration.CreateGlobalLimiter(
            new ApiRateLimitOptions { CredentialRequestsPerMinutePerIp = 1 }
        );
        var (executing, queued) = await FillPasswordHashingPermitsAsync(sut);

        var overBudget = Acquire(sut, PasswordHashingRequest(Address(executing.Count + 1)));

        queued.IsCompleted.Should().BeFalse();
        overBudget.IsCompleted.Should().BeTrue();
        using (var refused = await overBudget)
        {
            refused.IsAcquired.Should().BeFalse();
            refused.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter).Should().BeTrue();
            retryAfter.Should().Be(TimeSpan.FromSeconds(10));
        }

        Release(executing);
        using var admitted = await queued;
        admitted.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task GlobalLimiterAdmitsOtherRequestsWhilePasswordHashingRequestsWait()
    {
        using var sut = ApiRateLimitConfiguration.CreateGlobalLimiter(new ApiRateLimitOptions());
        var (executing, firstQueued) = await FillPasswordHashingPermitsAsync(sut);
        var queued = new List<Task<RateLimitLease>> { firstQueued };
        for (var host = executing.Count + 2; host < 255; host++)
        {
            var request = Acquire(sut, PasswordHashingRequest(Address(host)));
            if (request.IsCompleted)
            {
                using var refused = await request;
                refused.IsAcquired.Should().BeFalse();
                break;
            }

            queued.Add(request);
        }

        var other = Acquire(sut, Request(IPAddress.Loopback));

        queued.Should().HaveCountGreaterThan(1);
        other.IsCompleted.Should().BeTrue();
        using (var admittedOther = await other)
        {
            admittedOther.IsAcquired.Should().BeTrue();
        }

        Release(executing);
        foreach (var request in queued)
        {
            using var admitted = await request;
            admitted.IsAcquired.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GlobalLimiterAdmitsPasswordHashingRequestsWhileOtherRequestsWait()
    {
        using var sut = ApiRateLimitConfiguration.CreateGlobalLimiter(new ApiRateLimitOptions());
        var executing = new List<RateLimitLease>();
        Task<RateLimitLease>? queued = null;
        while (queued is null)
        {
            var request = Acquire(sut, Request(IPAddress.Loopback));
            if (request.IsCompleted)
            {
                executing.Add(await request);
            }
            else
            {
                queued = request;
            }
        }

        var hashing = Acquire(sut, PasswordHashingRequest(Address(1)));

        executing.Should().AllSatisfy(lease => lease.IsAcquired.Should().BeTrue());
        hashing.IsCompleted.Should().BeTrue();
        using (var admitted = await hashing)
        {
            admitted.IsAcquired.Should().BeTrue();
        }

        Release(executing);
        using var admittedQueued = await queued;
        admittedQueued.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task GlobalLimiterEnforcesEveryPolicyBudget()
    {
        using var sut = ApiRateLimitConfiguration.CreateGlobalLimiter(
            new ApiRateLimitOptions
            {
                CredentialRequestsPerMinutePerIp = 1,
                ReportRequestsPerMinutePerUser = 1,
                SingleRecipientEmailRequestsPerMinutePerUser = 1,
                BulkEmailRequestsPerMinutePerUser = 1,
                FileUploadRequestsPerMinutePerUser = 1,
            }
        );
        var policies = typeof(SecurityPolicies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        policies.Should().NotBeEmpty();
        foreach (var policy in policies)
        {
            using var accepted = await Acquire(
                sut,
                Request(IPAddress.Loopback, new EnableRateLimitingAttribute(policy))
            );
            using var refused = await Acquire(
                sut,
                Request(IPAddress.Loopback, new EnableRateLimitingAttribute(policy))
            );

            accepted.IsAcquired.Should().BeTrue(policy);
            refused.IsAcquired.Should().BeFalse(policy);
        }
    }

    private static async Task<(
        List<RateLimitLease> Executing,
        Task<RateLimitLease> Queued
    )> FillPasswordHashingPermitsAsync(PartitionedRateLimiter<HttpContext> limiter)
    {
        var executing = new List<RateLimitLease>();
        for (var host = 1; host < 255; host++)
        {
            var request = Acquire(limiter, PasswordHashingRequest(Address(host)));
            if (!request.IsCompleted)
            {
                executing.Should().NotBeEmpty();
                return (executing, request);
            }

            var lease = await request;
            lease.IsAcquired.Should().BeTrue();
            executing.Add(lease);
        }

        throw new InvalidOperationException("The password hashing permits never ran out.");
    }

    private static Task<RateLimitLease> Acquire(
        PartitionedRateLimiter<HttpContext> limiter,
        HttpContext request
    )
    {
        return limiter
            .AcquireAsync(request, cancellationToken: TestContext.Current.CancellationToken)
            .AsTask();
    }

    private static void Release(List<RateLimitLease> leases)
    {
        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }

    private static IPAddress Address(int host)
    {
        return new IPAddress([192, 0, 2, (byte)host]);
    }

    private static DefaultHttpContext PasswordHashingRequest(IPAddress address)
    {
        return Request(
            address,
            new EnableRateLimitingAttribute(SecurityPolicies.Credentials),
            new PasswordHashingAttribute()
        );
    }

    private static DefaultHttpContext Request(IPAddress address, params object[] metadata)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;
        context.SetEndpoint(
            new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test")
        );
        return context;
    }
}
