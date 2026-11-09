using System.Threading.RateLimiting;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class RetryAfterRateLimiterTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public void AttemptAcquireRefusalReportsTheWait()
    {
        using var sut = new RetryAfterRateLimiter(Concurrency(), Wait);

        using var acquired = sut.AttemptAcquire(new DefaultHttpContext());
        using var refused = sut.AttemptAcquire(new DefaultHttpContext());

        acquired.IsAcquired.Should().BeTrue();
        RetryAfter(acquired).Should().BeNull();
        refused.IsAcquired.Should().BeFalse();
        RetryAfter(refused).Should().Be(Wait);
        refused.MetadataNames.Should().Contain(MetadataName.RetryAfter.Name);
    }

    [Fact]
    public async Task AcquireAsyncRefusalReportsTheWait()
    {
        using var sut = new RetryAfterRateLimiter(Concurrency(), Wait);

        using var acquired = await AcquireAsync(sut);
        using var refused = await AcquireAsync(sut);

        acquired.IsAcquired.Should().BeTrue();
        refused.IsAcquired.Should().BeFalse();
        RetryAfter(refused).Should().Be(Wait);
    }

    [Fact]
    public void RefusalWithItsOwnWaitPassesUnchanged()
    {
        var ownWait = TimeSpan.FromMinutes(5);
        using var sut = new RetryAfterRateLimiter(
            PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetTokenBucketLimiter(
                    "bucket",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 1,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = ownWait,
                        QueueLimit = 0,
                    }
                )
            ),
            Wait
        );

        using var acquired = sut.AttemptAcquire(new DefaultHttpContext());
        using var refused = sut.AttemptAcquire(new DefaultHttpContext());

        refused.IsAcquired.Should().BeFalse();
        RetryAfter(refused).Should().Be(ownWait);
    }

    [Fact]
    public void DisposeDisposesTheWrappedLimiter()
    {
        var limiter = Concurrency();
        var sut = new RetryAfterRateLimiter(limiter, Wait);

        sut.Dispose();

        limiter
            .Invoking(disposed => disposed.AttemptAcquire(new DefaultHttpContext()))
            .Should()
            .Throw<ObjectDisposedException>();
    }

    private static PartitionedRateLimiter<HttpContext> Concurrency()
    {
        return ClientRequestRateLimiter.CreateConcurrency(1, 0);
    }

    private static Task<RateLimitLease> AcquireAsync(PartitionedRateLimiter<HttpContext> limiter)
    {
        return limiter
            .AcquireAsync(
                new DefaultHttpContext(),
                cancellationToken: TestContext.Current.CancellationToken
            )
            .AsTask();
    }

    private static TimeSpan? RetryAfter(RateLimitLease lease)
    {
        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? retryAfter
            : null;
    }
}
