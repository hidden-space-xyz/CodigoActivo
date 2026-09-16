using System.Globalization;
using System.Threading.RateLimiting;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Configuration;

internal static class ApiRateLimitConfiguration
{
    private const int MaxConcurrentApiRequests = 128;
    private const int MaxQueuedApiRequests = 256;
    private const int MaxConcurrentCredentialRequests = 4;
    private const int MaxQueuedCredentialRequests = 16;
    private const int MaxConcurrentReportRequests = 16;
    private const int MaxQueuedReportRequests = 32;
    private const int MaxConcurrentSingleRecipientEmailRequests = 10;
    private const int MaxQueuedSingleRecipientEmailRequests = 10;
    private const int MaxConcurrentBulkEmailRequests = 2;
    private const int MaxQueuedBulkEmailRequests = 0;
    private const int MaxConcurrentFileUploadRequests = 12;
    private const int MaxQueuedFileUploadRequests = 12;

    internal static void AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddSingleton(new ApiRateLimitOptions());
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = GetRetryAfter(context.Lease);
                return ValueTask.CompletedTask;
            };
        });
        services.AddOptions<RateLimiterOptions>().Configure<ApiRateLimitOptions>(ConfigureLimiters);
    }

    private static void ConfigureLimiters(RateLimiterOptions options, ApiRateLimitOptions limits)
    {
        options.GlobalLimiter = CreateGlobalLimiter(limits);

        options.AddPolicy(
            SecurityPolicies.Credentials,
            context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ =>
                        new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = limits.CredentialRequestsPerMinutePerIp,
                            Window = TimeSpan.FromMinutes(1),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }
                )
        );
        options.AddPolicy(
            SecurityPolicies.Reports,
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                limits.ReportRequestsPerMinutePerUser
            )
        );
        options.AddPolicy(
            SecurityPolicies.SingleRecipientEmail,
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                limits.SingleRecipientEmailRequestsPerMinutePerUser
            )
        );
        options.AddPolicy(
            SecurityPolicies.BulkEmail,
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                limits.BulkEmailRequestsPerMinutePerUser
            )
        );
        options.AddPolicy(
            SecurityPolicies.FileUploads,
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                limits.FileUploadRequestsPerMinutePerUser
            )
        );
    }

    private static string GetRetryAfter(RateLimitLease lease)
    {
        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            && retryAfter > TimeSpan.Zero
            ? Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture)
            : "1";
    }

    private static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(
        ApiRateLimitOptions limits
    )
    {
        return PartitionedRateLimiter.CreateChained(
            ClientRequestRateLimiter.CreateGlobal(
                limits.AuthenticatedRequestsPerMinute,
                limits.AnonymousRequestsPerMinutePerIp
            ),
            ClientRequestRateLimiter.CreateConcurrency(
                MaxConcurrentApiRequests,
                MaxQueuedApiRequests
            ),
            EndpointConcurrencyLimiter.Create(
                SecurityPolicies.Credentials,
                MaxConcurrentCredentialRequests,
                MaxQueuedCredentialRequests
            ),
            EndpointConcurrencyLimiter.Create(
                SecurityPolicies.Reports,
                MaxConcurrentReportRequests,
                MaxQueuedReportRequests
            ),
            EndpointConcurrencyLimiter.Create(
                SecurityPolicies.SingleRecipientEmail,
                MaxConcurrentSingleRecipientEmailRequests,
                MaxQueuedSingleRecipientEmailRequests
            ),
            EndpointConcurrencyLimiter.Create(
                SecurityPolicies.BulkEmail,
                MaxConcurrentBulkEmailRequests,
                MaxQueuedBulkEmailRequests
            ),
            EndpointConcurrencyLimiter.Create(
                SecurityPolicies.FileUploads,
                MaxConcurrentFileUploadRequests,
                MaxQueuedFileUploadRequests
            )
        );
    }
}
