using CodigoActivo.Application.BlockedData.Commands;
using CodigoActivo.Application.TermsDecisions.Commands;
using CodigoActivo.Infrastructure.Diagnostics;
using CodigoActivo.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Legal;

/// <summary>
/// Tells the association's legal contact about the consents withdrawn and the consultations of the
/// blocked data opened, each kind in one email per run. What it tells waits in the database until
/// the email is queued, so a stopped API only delays the notices until the next start.
/// </summary>
/// <param name="scopes">Scope factory used to resolve the use cases of each run.</param>
/// <param name="options">Startup delay and interval of the runs.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class LegalNoticeWorker(
    IServiceScopeFactory scopes,
    LegalNoticeOptions options,
    ILogger<LegalNoticeWorker> logger
) : BackgroundService
{
    /// <summary>
    /// Sends the notice of the withdrawn consents and then that of the opened consultations.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result contains the number of withdrawals and of consultations told.
    /// </returns>
    public async Task<(int Withdrawals, int Consultations)> NotifyAsync(
        CancellationToken ct = default
    )
    {
        var withdrawals = await ScheduledCommandRunner.RunAsync(
            scopes,
            new SendConsentWithdrawalNoticesCommand(),
            ct
        );
        var consultations = await ScheduledCommandRunner.RunAsync(
            scopes,
            new SendBlockedDataConsultationNoticesCommand(),
            ct
        );
        return (withdrawals, consultations);
    }

    /// <summary>
    /// Runs shortly after startup and then once per configured interval until shutdown. A failed
    /// run is logged and never stops the host or the following runs.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return ScheduledCommandRunner.RepeatAsync(
            options.StartupDelay,
            options.Interval,
            NotifyAsync,
            logger.LegalNoticeRunFailed,
            stoppingToken
        );
    }
}
