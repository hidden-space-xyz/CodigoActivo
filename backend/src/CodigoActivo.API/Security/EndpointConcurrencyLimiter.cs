using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Security;

/// <summary>
/// Creates process-wide concurrency limits for endpoints that use a named rate-limit policy or
/// carry a marker in their metadata.
/// </summary>
public static class EndpointConcurrencyLimiter
{
    /// <summary>
    /// Creates a concurrency limiter for the selected endpoint policy.
    /// </summary>
    /// <param name="policyName">Policy whose endpoints consume the shared capacity.</param>
    /// <param name="permitLimit">Requests allowed to execute concurrently.</param>
    /// <param name="queueLimit">Requests allowed to wait for execution capacity.</param>
    /// <returns>The configured partitioned limiter.</returns>
    public static PartitionedRateLimiter<HttpContext> Create(
        string policyName,
        int permitLimit,
        int queueLimit
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        return Create(
            policyName,
            metadata =>
                string.Equals(
                    metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName,
                    policyName,
                    StringComparison.Ordinal
                ),
            permitLimit,
            queueLimit
        );
    }

    /// <summary>
    /// Creates a concurrency limiter for the endpoints whose metadata carries a marker.
    /// </summary>
    /// <typeparam name="TMarker">Metadata that marks the endpoints sharing the capacity.</typeparam>
    /// <param name="permitLimit">Requests allowed to execute concurrently.</param>
    /// <param name="queueLimit">Requests allowed to wait for execution capacity.</param>
    /// <returns>The configured partitioned limiter.</returns>
    public static PartitionedRateLimiter<HttpContext> CreateForMarker<TMarker>(
        int permitLimit,
        int queueLimit
    )
        where TMarker : class
    {
        return Create(
            typeof(TMarker).Name,
            metadata => metadata.GetMetadata<TMarker>() is not null,
            permitLimit,
            queueLimit
        );
    }

    private static PartitionedRateLimiter<HttpContext> Create(
        string partitionName,
        Func<EndpointMetadataCollection, bool> selects,
        int permitLimit,
        int queueLimit
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(queueLimit);

        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.GetEndpoint()?.Metadata is { } metadata && selects(metadata)
                ? RateLimitPartition.GetConcurrencyLimiter(
                    partitionName,
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    }
                )
                : RateLimitPartition.GetNoLimiter("unlimited")
        );
    }
}
