using System.Globalization;
using System.Threading.RateLimiting;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace CodigoActivo.API.Configuration;

internal static class ApiRateLimitConfiguration
{
    private const int AuthenticatedRequestsPerMinute = 300;
    private const int AnonymousRequestsPerMinutePerIp = 3_000;
    private const int CredentialRequestsPerMinutePerIp = 120;
    private const int ReportRequestsPerMinutePerUser = 30;
    private const int SingleRecipientEmailRequestsPerMinutePerUser = 30;
    private const int BulkEmailRequestsPerMinutePerUser = 5;
    private const int FileUploadRequestsPerMinutePerUser = 30;
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
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = GetRetryAfter(context.Lease);
                return ValueTask.CompletedTask;
            };
            options.GlobalLimiter = CreateGlobalLimiter();

            options.AddPolicy(
                SecurityPolicies.Credentials,
                context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ =>
                            new SlidingWindowRateLimiterOptions
                            {
                                PermitLimit = CredentialRequestsPerMinutePerIp,
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
                    ReportRequestsPerMinutePerUser
                )
            );
            options.AddPolicy(
                SecurityPolicies.SingleRecipientEmail,
                ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                    SingleRecipientEmailRequestsPerMinutePerUser
                )
            );
            options.AddPolicy(
                SecurityPolicies.BulkEmail,
                ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                    BulkEmailRequestsPerMinutePerUser
                )
            );
            options.AddPolicy(
                SecurityPolicies.FileUploads,
                ClientRequestRateLimiter.CreateAuthenticatedPolicy(
                    FileUploadRequestsPerMinutePerUser
                )
            );
        });
    }

    private static string GetRetryAfter(RateLimitLease lease)
    {
        return lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            && retryAfter > TimeSpan.Zero
            ? Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture)
            : "1";
    }

    private static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter()
    {
        return PartitionedRateLimiter.CreateChained(
            ClientRequestRateLimiter.CreateGlobal(
                AuthenticatedRequestsPerMinute,
                AnonymousRequestsPerMinutePerIp
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
