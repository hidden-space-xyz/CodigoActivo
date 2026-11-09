using System.Globalization;
using System.Threading.RateLimiting;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Configuration;

internal static class ApiRateLimitConfiguration
{
    private const int MaxConcurrentApiRequests = 64;
    private const int MaxQueuedApiRequests = 2048;
    private const int MaxConcurrentPasswordHashingRequests = 4;
    private const int MaxQueuedPasswordHashingRequests = 64;
    private const int MaxConcurrentReportRequests = 8;
    private const int MaxQueuedReportRequests = 40;
    private const int MaxConcurrentSingleRecipientEmailRequests = 10;
    private const int MaxQueuedSingleRecipientEmailRequests = 10;
    private const int MaxConcurrentBulkEmailRequests = 2;
    private const int MaxQueuedBulkEmailRequests = 0;
    private const int MaxConcurrentFileUploadRequests = 4;
    private const int MaxQueuedFileUploadRequests = 20;

    internal static void AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddSingleton(new ApiRateLimitOptions());
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = GetRetryAfter(context.Lease);
                await context.HttpContext.WriteApiErrorAsync(
                    ApiErrorResponseExtensions.CreateTooManyRequests(context.HttpContext),
                    ct
                );
            };
        });
        services.AddOptions<RateLimiterOptions>().Configure<ApiRateLimitOptions>(ConfigureLimiters);
    }

    private static void ConfigureLimiters(RateLimiterOptions options, ApiRateLimitOptions limits)
    {
        options.GlobalLimiter = CreateGlobalLimiter(limits);

        foreach (
            var policy in (string[])
                [
                    SecurityPolicies.Credentials,
                    SecurityPolicies.Reports,
                    SecurityPolicies.SingleRecipientEmail,
                    SecurityPolicies.BulkEmail,
                    SecurityPolicies.FileUploads,
                ]
        )
        {
            options.AddPolicy(policy, _ => RateLimitPartition.GetNoLimiter(policy));
        }
    }

    private static string GetRetryAfter(RateLimitLease lease)
    {
        return
            lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            && retryAfter > TimeSpan.Zero
            ? Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture)
            : "1";
    }

    internal static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(
        ApiRateLimitOptions limits
    )
    {
        return new SinglePassChainedRateLimiter(
            ClientRequestRateLimiter.CreateGlobal(
                limits.AuthenticatedRequestsPerMinute,
                limits.AnonymousRequestsPerMinutePerIp
            ),
            ClientRequestRateLimiter.CreateIpPolicy(
                SecurityPolicies.Credentials,
                limits.CredentialRequestsPerMinutePerIp
            ),
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                SecurityPolicies.Reports,
                limits.ReportRequestsPerMinutePerUser
            ),
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                SecurityPolicies.SingleRecipientEmail,
                limits.SingleRecipientEmailRequestsPerMinutePerUser
            ),
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                SecurityPolicies.BulkEmail,
                limits.BulkEmailRequestsPerMinutePerUser
            ),
            ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                SecurityPolicies.FileUploads,
                limits.FileUploadRequestsPerMinutePerUser
            ),
            EndpointConcurrencyLimiter.CreateForMarker<PasswordHashingAttribute>(
                MaxConcurrentPasswordHashingRequests,
                MaxQueuedPasswordHashingRequests
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
            ),
            ClientRequestRateLimiter.CreateConcurrency(
                MaxConcurrentApiRequests,
                MaxQueuedApiRequests
            )
        );
    }
}
