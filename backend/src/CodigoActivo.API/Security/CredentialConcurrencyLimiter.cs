using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Security;

public static class CredentialConcurrencyLimiter
{
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
