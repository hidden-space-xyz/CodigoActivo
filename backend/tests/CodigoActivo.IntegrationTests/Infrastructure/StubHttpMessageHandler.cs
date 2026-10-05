using System.Net;
using System.Text;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> responses = new();

    public void Enqueue(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        responses.Enqueue((status, body));
    }

    protected override HttpResponseMessage Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var (status, body) = responses.Dequeue();
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
        return Task.FromResult(Send(request, cancellationToken));
    }
}
