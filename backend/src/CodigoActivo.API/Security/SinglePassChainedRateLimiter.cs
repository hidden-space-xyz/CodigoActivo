using System.Threading.RateLimiting;

namespace CodigoActivo.API.Security;

/// <summary>
/// Chains limiters so the rate-limiting middleware acquires them in a single pass. The middleware
/// first attempts without waiting and, when that fails, acquires again waiting, so a chain that
/// spent a per-minute permit before a concurrency limiter refused the attempt would count the
/// request twice. Here the attempt never acquires, and every request is counted at most once by
/// each limiter, whether it executes, waits or is refused. Disposing it disposes the chained
/// limiters.
/// </summary>
internal sealed class SinglePassChainedRateLimiter : PartitionedRateLimiter<HttpContext>
{
    private readonly PartitionedRateLimiter<HttpContext>[] limiters;
    private readonly PartitionedRateLimiter<HttpContext> chain;

    /// <summary>
    /// Initializes the chain, acquired in the order given.
    /// </summary>
    /// <param name="limiters">Limiters to acquire, in order.</param>
    public SinglePassChainedRateLimiter(params PartitionedRateLimiter<HttpContext>[] limiters)
    {
        this.limiters = limiters;
        chain = PartitionedRateLimiter.CreateChained(limiters);
    }

    /// <inheritdoc />
    public override RateLimiterStatistics? GetStatistics(HttpContext resource)
    {
        return chain.GetStatistics(resource);
    }

    /// <inheritdoc />
    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount)
    {
        return NotAttemptedLease.Instance;
    }

    /// <inheritdoc />
    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource,
        int permitCount,
        CancellationToken cancellationToken
    )
    {
        return chain.AcquireAsync(resource, permitCount, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            chain.Dispose();
            foreach (var limiter in limiters)
            {
                limiter.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private sealed class NotAttemptedLease : RateLimitLease
    {
        internal static readonly NotAttemptedLease Instance = new();

        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }
}
