using System.Threading.RateLimiting;
using CodigoActivo.API.Extensions;

namespace CodigoActivo.API.Security;

/// <summary>
/// Creates application-aware request limiters partitioned by authenticated user or client IP.
/// </summary>
public static class ClientRequestRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Creates the global per-client limiter used by all API endpoints.
    /// </summary>
    /// <param name="authenticatedPermitLimit">Requests allowed per authenticated user and window.</param>
    /// <param name="anonymousPermitLimit">Requests allowed per anonymous IP and window.</param>
    /// <returns>The configured partitioned limiter.</returns>
    public static PartitionedRateLimiter<HttpContext> CreateGlobal(
        int authenticatedPermitLimit,
        int anonymousPermitLimit
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(authenticatedPermitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(anonymousPermitLimit, 1);

        return PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var userId = context.User.GetUserId();
            var key = userId is null
                ? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}"
                : $"user:{userId.Value:N}";
            var permitLimit = userId is null
                ? anonymousPermitLimit
                : authenticatedPermitLimit;

            return SlidingWindow(key, permitLimit);
        });
    }

    /// <summary>
    /// Creates a process-wide concurrency limiter for API requests.
    /// </summary>
    /// <param name="permitLimit">Requests allowed to execute concurrently.</param>
    /// <param name="queueLimit">Requests allowed to wait for execution capacity.</param>
    /// <returns>The configured partitioned limiter.</returns>
    public static PartitionedRateLimiter<HttpContext> CreateConcurrency(
        int permitLimit,
        int queueLimit
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(queueLimit);

        return PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetConcurrencyLimiter(
                "api",
                _ =>
                    new ConcurrencyLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        QueueLimit = queueLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    }
            )
        );
    }

    /// <summary>
    /// Creates a named per-user policy for comparatively expensive endpoints.
    /// </summary>
    /// <param name="permitLimit">Requests allowed per user and window.</param>
    /// <returns>A partition factory for the named policy.</returns>
    public static Func<
        HttpContext,
        RateLimitPartition<string>
    > CreateAuthenticatedPolicy(int permitLimit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(permitLimit, 1);

        return context =>
        {
            var key = context.User.GetUserId() is { } userId
                ? $"user:{userId:N}"
                : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return SlidingWindow(key, permitLimit);
        };
    }

    private static RateLimitPartition<string> SlidingWindow(string key, int permitLimit)
    {
        return RateLimitPartition.GetSlidingWindowLimiter(
            key,
            _ =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = Window,
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }
        );
    }
}
