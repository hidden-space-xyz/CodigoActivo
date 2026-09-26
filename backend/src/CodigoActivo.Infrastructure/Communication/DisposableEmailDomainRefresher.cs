using CodigoActivo.Domain.Repositories;
using CodigoActivo.Infrastructure.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Keeps the stored disposable email domain list up to date. It downloads the list shortly after
/// startup and then once per refresh interval, and replaces the stored list only with a download
/// that passed validation. A failed download, a rejected list or a failed write is logged, leaves
/// the last valid list in place and is retried after the shorter retry interval. Nothing here ever
/// stops the host, and while no list has been stored every email domain is accepted.
/// </summary>
/// <param name="downloader">Component that downloads and validates the list.</param>
/// <param name="scopes">Scope factory used to resolve the scoped repository of each run.</param>
/// <param name="options">Configuration values used by the component.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class DisposableEmailDomainRefresher(
    DisposableEmailDomainDownloader downloader,
    IServiceScopeFactory scopes,
    DisposableEmailDomainOptions options,
    ILogger<DisposableEmailDomainRefresher> logger
) : BackgroundService
{
    /// <summary>
    /// Downloads the list and, when it is valid, stores it in place of the previous one. A download
    /// that fails or is rejected is logged and changes nothing.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the downloaded list was stored.</returns>
    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        DisposableEmailDomainListResult list;
        try
        {
            list = await downloader.DownloadAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.DisposableEmailDomainDownloadFailed(ex);
            return false;
        }

        if (list.Rejection is { } rejection)
        {
            logger.DisposableEmailDomainListRejected(rejection);
            return false;
        }

        await using var scope = scopes.CreateAsyncScope();
        var repository =
            scope.ServiceProvider.GetRequiredService<IDisposableEmailDomainRepository>();
        await repository.ReplaceAsync(list.Domains, ct);
        return true;
    }

    /// <summary>
    /// Refreshes after the startup delay and then keeps refreshing until shutdown, waiting the
    /// refresh interval after a stored list and the retry interval after any failure. Only the
    /// shutdown token ends the loop: a timeout or any other failure is logged and retried.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(options.StartupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var stored = await RefreshSafelyAsync(stoppingToken);
                await Task.Delay(
                    stored ? options.RefreshInterval : options.RetryInterval,
                    stoppingToken
                );
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
    }

    private async Task<bool> RefreshSafelyAsync(CancellationToken ct)
    {
        try
        {
            return await RefreshAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.DisposableEmailDomainListNotStored(ex);
            return false;
        }
    }
}
