using System.Net;
using AwesomeAssertions;
using CodigoActivo.Infrastructure.Communication;
using CodigoActivo.UnitTests.TestSupport;
using Xunit;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

public sealed class DisposableEmailDomainDownloaderTests
{
    private static readonly Uri Source = new("https://lists.example.test/disposable.conf");

    private static DisposableEmailDomainDownloader Create(
        StubHttpMessageHandler handler,
        TimeSpan? timeout = null
    )
    {
        return new DisposableEmailDomainDownloader(
            handler,
            new DisposableEmailDomainOptions
            {
                SourceUrl = Source,
                DownloadTimeout = timeout ?? TimeSpan.FromSeconds(30),
            }
        );
    }

    private static HttpResponseMessage Streaming(Stream body, long? declaredLength = null)
    {
        var content = new StreamContent(body);
        content.Headers.ContentLength = declaredLength;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    [Fact]
    public async Task DownloadAsyncGenuineListReturnsItsDomains()
    {
        var handler = StubHttpMessageHandler.Returning(() =>
            StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine("mailinator.com"))
        );
        using var downloader = Create(handler);

        var result = await downloader.DownloadAsync(TestContext.Current.CancellationToken);

        result.Rejection.Should().BeNull();
        result.Domains.Should().HaveCount(DisposableEmailDomainList.MinDomains + 1);
        result.Domains.Should().Contain("mailinator.com");
        handler.RequestedUris.Should().Equal(Source);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Moved)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task DownloadAsyncUnsuccessfulStatusThrowsHttpRequestException(
        HttpStatusCode status
    )
    {
        var handler = StubHttpMessageHandler.Returning(() =>
            StubHttpMessageHandler.Text(DisposableEmailDomainLists.Genuine(), status)
        );
        using var downloader = Create(handler);

        var download = () => downloader.DownloadAsync(TestContext.Current.CancellationToken);

        await download.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task DownloadAsyncDeclaredLengthOverTheMaximumIsRejectedUnread()
    {
        var body = new EndlessStream();
        var handler = StubHttpMessageHandler.Returning(() =>
            Streaming(body, DisposableEmailDomainList.MaxBytes + 1L)
        );
        using var downloader = Create(handler);

        var result = await downloader.DownloadAsync(TestContext.Current.CancellationToken);

        result.Rejection.Should().Be(DisposableEmailDomainListRejection.TooLarge);
        body.BytesRead.Should().Be(0);
    }

    [Fact]
    public async Task DownloadAsyncEndlessBodyIsRejectedOneByteOverTheMaximum()
    {
        var body = new EndlessStream();
        var handler = StubHttpMessageHandler.Returning(() => Streaming(body));
        using var downloader = Create(handler);

        var result = await downloader.DownloadAsync(TestContext.Current.CancellationToken);

        result.Rejection.Should().Be(DisposableEmailDomainListRejection.TooLarge);
        body.BytesRead.Should().Be(DisposableEmailDomainList.MaxBytes + 1L);
    }

    [Fact]
    public async Task DownloadAsyncSlowSourceTimesOutWithoutTheCallerCancelling()
    {
        var handler = new StubHttpMessageHandler(
            async (_, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
        );
        using var downloader = Create(handler, TimeSpan.FromMilliseconds(50));

        var download = () => downloader.DownloadAsync(TestContext.Current.CancellationToken);

        await download.Should().ThrowAsync<OperationCanceledException>();
        TestContext.Current.CancellationToken.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void DisposeReleasesTheOwnedHandler()
    {
        var handler = StubHttpMessageHandler.Returning(() => StubHttpMessageHandler.Text(""));
        var downloader = Create(handler);

        downloader.Dispose();

        handler.Disposed.Should().BeTrue();
    }

    private sealed class EndlessStream : Stream
    {
        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)'a', offset, count);
            BytesRead += count;
            return count;
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
