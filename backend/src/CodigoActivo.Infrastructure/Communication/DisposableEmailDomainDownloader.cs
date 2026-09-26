namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Downloads the disposable email domain list from the configured source and validates it through
/// <see cref="DisposableEmailDomainList"/>. The whole download, headers and body, is bounded by
/// <see cref="DisposableEmailDomainOptions.DownloadTimeout"/>, and no more than one byte over
/// <see cref="DisposableEmailDomainList.MaxBytes"/> is ever read, so an oversized or endless
/// response is rejected without being buffered.
/// </summary>
public sealed class DisposableEmailDomainDownloader : IDisposable
{
    private const int ChunkSize = 81920;

    private readonly HttpClient client;
    private readonly DisposableEmailDomainOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisposableEmailDomainDownloader"/> class.
    /// </summary>
    /// <param name="handler">Handler that sends the request; this instance owns and disposes it.</param>
    /// <param name="options">Configuration values used by the component.</param>
    public DisposableEmailDomainDownloader(
        HttpMessageHandler handler,
        DisposableEmailDomainOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        this.options = options;
    }

    /// <summary>
    /// Downloads and validates the list.
    /// </summary>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result contains the validated domains or why the list was rejected.</returns>
    /// <exception cref="HttpRequestException">
    /// The source could not be reached or did not answer with a success status.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// The download took longer than the configured timeout or <paramref name="ct"/> was cancelled.
    /// </exception>
    public async Task<DisposableEmailDomainListResult> DownloadAsync(CancellationToken ct = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.DownloadTimeout);

        using var response = await client.GetAsync(
            options.SourceUrl,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token
        );
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength > DisposableEmailDomainList.MaxBytes)
        {
            return DisposableEmailDomainListResult.Rejected(
                DisposableEmailDomainListRejection.TooLarge
            );
        }

        await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
        var content = await ReadAtMostAsync(
            body,
            DisposableEmailDomainList.MaxBytes + 1,
            timeout.Token
        );
        return DisposableEmailDomainList.Parse(content);
    }

    /// <summary>
    /// Releases the HTTP client and the handler it owns.
    /// </summary>
    public void Dispose()
    {
        client.Dispose();
    }

    private static async Task<byte[]> ReadAtMostAsync(Stream body, int limit, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkSize];
        while (buffer.Length < limit)
        {
            var wanted = (int)Math.Min(chunk.Length, limit - buffer.Length);
            var read = await body.ReadAsync(chunk.AsMemory(0, wanted), ct);
            if (read is 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }

        return buffer.ToArray();
    }
}
