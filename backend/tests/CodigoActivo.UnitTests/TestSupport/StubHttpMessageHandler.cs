using System.Net;
using System.Text;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
) : HttpMessageHandler
{
    private readonly List<Uri?> requestedUris = [];

    public IReadOnlyList<Uri?> RequestedUris
    {
        get
        {
            lock (requestedUris)
            {
                return [.. requestedUris];
            }
        }
    }

    public bool Disposed { get; private set; }

    public static StubHttpMessageHandler Returning(params Func<HttpResponseMessage>[] responses)
    {
        var next = 0;
        return new StubHttpMessageHandler(
            (_, _) =>
            {
                var index = Math.Min(Interlocked.Increment(ref next) - 1, responses.Length - 1);
                return Task.FromResult(responses[index]());
            }
        );
    }

    public static HttpResponseMessage Text(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        };
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        lock (requestedUris)
        {
            requestedUris.Add(request.RequestUri);
        }

        return respond(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}
