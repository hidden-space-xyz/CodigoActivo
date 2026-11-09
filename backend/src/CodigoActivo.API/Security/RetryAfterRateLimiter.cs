using System.Threading.RateLimiting;

namespace CodigoActivo.API.Security;

/// <summary>
/// Reports how long a refused client should wait for a limiter whose refusals carry no wait, such
/// as a sliding window: a refused lease reports <see cref="MetadataName.RetryAfter"/>, which the
/// rejection turns into the <c>Retry-After</c> header. Acquired leases and refusals that already
/// carry a wait pass unchanged. Disposing it disposes the wrapped limiter.
/// </summary>
internal sealed class RetryAfterRateLimiter : PartitionedRateLimiter<HttpContext>
{
    private readonly PartitionedRateLimiter<HttpContext> limiter;
    private readonly TimeSpan retryAfter;

    /// <summary>
    /// Initializes the limiter.
    /// </summary>
    /// <param name="limiter">Limiter whose refusals report the wait.</param>
    /// <param name="retryAfter">Wait reported by every refusal that carries none.</param>
    public RetryAfterRateLimiter(PartitionedRateLimiter<HttpContext> limiter, TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryAfter, TimeSpan.Zero);

        this.limiter = limiter;
        this.retryAfter = retryAfter;
    }

    /// <inheritdoc />
    public override RateLimiterStatistics? GetStatistics(HttpContext resource)
    {
        return limiter.GetStatistics(resource);
    }

    /// <inheritdoc />
    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount)
    {
        return WithRetryAfter(limiter.AttemptAcquire(resource, permitCount));
    }

    /// <inheritdoc />
    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource,
        int permitCount,
        CancellationToken cancellationToken
    )
    {
        return WithRetryAfter(await limiter.AcquireAsync(resource, permitCount, cancellationToken));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            limiter.Dispose();
        }

        base.Dispose(disposing);
    }

    private RateLimitLease WithRetryAfter(RateLimitLease lease)
    {
        return lease.IsAcquired || lease.TryGetMetadata(MetadataName.RetryAfter, out _)
            ? lease
            : new RefusedLease(lease, retryAfter);
    }

    private sealed class RefusedLease(RateLimitLease lease, TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames =>
            [MetadataName.RetryAfter.Name, .. lease.MetadataNames];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (string.Equals(metadataName, MetadataName.RetryAfter.Name, StringComparison.Ordinal))
            {
                metadata = retryAfter;
                return true;
            }

            return lease.TryGetMetadata(metadataName, out metadata);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lease.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
