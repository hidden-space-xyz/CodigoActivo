using System.Net;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class SinglePassChainedRateLimiterTests
{
    [Fact]
    public async Task AttemptThenAcquireCountsAWaitingRequestOnce()
    {
        using var sut = new SinglePassChainedRateLimiter(
            ClientRequestRateLimiter.CreateGlobal(1, 2),
            ClientRequestRateLimiter.CreateConcurrency(1, 1)
        );

        var executing = await AttemptThenAcquireAsync(sut, NewContext());
        var waiting = AttemptThenAcquireAsync(sut, NewContext());

        executing.IsAcquired.Should().BeTrue();
        waiting.IsCompleted.Should().BeFalse();
        executing.Dispose();
        using var admitted = await waiting;
        admitted.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task AttemptAcquireNeverAcquiresNorSpendsAPermit()
    {
        using var sut = new SinglePassChainedRateLimiter(
            ClientRequestRateLimiter.CreateGlobal(1, 1)
        );

        using var attempt = sut.AttemptAcquire(NewContext());
        using var acquired = await sut.AcquireAsync(
            NewContext(),
            cancellationToken: TestContext.Current.CancellationToken
        );

        attempt.IsAcquired.Should().BeFalse();
        acquired.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public void DisposeDisposesTheChainedLimiters()
    {
        var limiter = ClientRequestRateLimiter.CreateConcurrency(1, 0);
        var sut = new SinglePassChainedRateLimiter(limiter);

        sut.Dispose();

        limiter
            .Invoking(disposed => disposed.AttemptAcquire(NewContext()))
            .Should()
            .Throw<ObjectDisposedException>();
    }

    private static async Task<RateLimitLease> AttemptThenAcquireAsync(
        PartitionedRateLimiter<HttpContext> limiter,
        HttpContext context
    )
    {
        var attempt = limiter.AttemptAcquire(context);
        if (attempt.IsAcquired)
        {
            return attempt;
        }

        attempt.Dispose();
        return await limiter.AcquireAsync(
            context,
            cancellationToken: TestContext.Current.CancellationToken
        );
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        return context;
    }
}
