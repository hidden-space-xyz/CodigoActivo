using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Security;

/// <summary>
/// Creates process-wide concurrency limits for endpoints that use a named rate-limit policy.
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
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(queueLimit);

        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var endpointPolicy = context
                .GetEndpoint()
                ?.Metadata.GetMetadata<EnableRateLimitingAttribute>()
                ?.PolicyName;

            return string.Equals(endpointPolicy, policyName, StringComparison.Ordinal)
                ? RateLimitPartition.GetConcurrencyLimiter(
                    policyName,
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    }
                )
                : RateLimitPartition.GetNoLimiter("unlimited");
        });
    }
}
