using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Security;

/// <summary>
/// Enforces the configured limits for credential concurrency.
/// </summary>
public static class CredentialConcurrencyLimiter
{
    /// <summary>
    /// Creates a credential concurrency limiter from the validated request.
    /// </summary>
    /// <param name="permitLimit">Number of permit allowed or reported.</param>
    /// <param name="queueLimit">Number of queue allowed or reported.</param>
    /// <returns>The resulting http context value.</returns>
    public static PartitionedRateLimiter<HttpContext> Create(int permitLimit, int queueLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(queueLimit);

        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var policy = context
                .GetEndpoint()
                ?.Metadata.GetMetadata<EnableRateLimitingAttribute>()
                ?.PolicyName;

            return string.Equals(policy, SecurityPolicies.Credentials, StringComparison.Ordinal)
                ? RateLimitPartition.GetConcurrencyLimiter(
                    SecurityPolicies.Credentials,
                    _ =>
                        new ConcurrencyLimiterOptions
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
