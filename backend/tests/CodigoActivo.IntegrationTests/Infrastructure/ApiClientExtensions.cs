using System.Net.Http.Json;
using CodigoActivo.Application.DTOs;

namespace CodigoActivo.IntegrationTests.Infrastructure;

/// <summary>
/// HTTP helpers that mirror how the SPA talks to the API: a fresh CSRF token is fetched from
/// <c>GET /api/auth/csrf</c> (which also drops the antiforgery cookie) immediately before each
/// unsafe request, so the token always matches the current identity and cookie. Bodies and responses
/// use <see cref="TestJson.Options"/>.
/// </summary>
public static class ApiClientExtensions
{
    public static async Task<string> FetchCsrfTokenAsync(
        this HttpClient client,
        CancellationToken ct = default
    )
    {
        using var response = await client.GetAsync("/api/auth/csrf", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>(TestJson.Options, ct);
        return body!.Token;
    }

    public static async Task<HttpResponseMessage> SendWithCsrfAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? body = null,
        CancellationToken ct = default
    )
    {
        var token = await client.FetchCsrfTokenAsync(ct);
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-CSRF-TOKEN", token);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType(), mediaType: null, TestJson.Options);

        return await client.SendAsync(request, ct);
    }

    public static Task<HttpResponseMessage> PostJsonAsync(
        this HttpClient client,
        string url,
        object? body,
        CancellationToken ct = default
    )
    {
        return client.SendWithCsrfAsync(HttpMethod.Post, url, body, ct);
    }

    public static Task<HttpResponseMessage> PutJsonAsync(
        this HttpClient client,
        string url,
        object? body,
        CancellationToken ct = default
    )
    {
        return client.SendWithCsrfAsync(HttpMethod.Put, url, body, ct);
    }

    public static Task<HttpResponseMessage> PatchJsonAsync(
        this HttpClient client,
        string url,
        object? body = null,
        CancellationToken ct = default
    )
    {
        return client.SendWithCsrfAsync(HttpMethod.Patch, url, body, ct);
    }

    public static Task<HttpResponseMessage> DeleteWithCsrfAsync(
        this HttpClient client,
        string url,
        CancellationToken ct = default
    )
    {
        return client.SendWithCsrfAsync(HttpMethod.Delete, url, body: null, ct);
    }

    public static async Task<T?> ReadJsonAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default
    )
    {
        return await response.Content.ReadFromJsonAsync<T>(TestJson.Options, ct);
    }
}
