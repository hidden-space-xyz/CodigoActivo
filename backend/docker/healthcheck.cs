#:property PublishAot=false

using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
using var request = new HttpRequestMessage(HttpMethod.Get, args[0]);
foreach (var header in args[1..])
{
    var separator = header.IndexOf(':', StringComparison.Ordinal);
    request.Headers.Add(header[..separator], header[(separator + 1)..].Trim());
}

try
{
    using var response = await client.SendAsync(request);
    return response.IsSuccessStatusCode ? 0 : 1;
}
catch (HttpRequestException)
{
    return 1;
}
catch (TaskCanceledException)
{
    return 1;
}
