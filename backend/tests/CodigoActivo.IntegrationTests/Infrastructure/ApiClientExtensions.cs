using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Application.DTOs;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public static class ApiClientExtensions
{
    public static async Task<string> FetchCsrfTokenAsync(
        this HttpClient client,
        CancellationToken ct = default
    )
    {
        using var response = await client.GetAsync("/api/auth/csrf", ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>(
            TestJson.Options,
            ct
        );
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
            request.Content = JsonContent.Create(
                body,
                body.GetType(),
                mediaType: null,
                TestJson.Options
            );

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

    public static async Task<HttpResponseMessage> SendUploadAsync(
        this HttpClient client,
        HttpMethod method,
        string url,
        byte[]? fileBytes,
        string fileName = "image.png",
        string partContentType = "image/png",
        bool withCsrf = true
    )
    {
        using var request = new HttpRequestMessage(method, url);
        if (withCsrf)
        {
            var token = await client.FetchCsrfTokenAsync(TestCancellation.Ct);
            request.Headers.Add("X-CSRF-TOKEN", token);
        }

        var form = new MultipartFormDataContent();
        if (fileBytes is not null)
        {
            var part = new ByteArrayContent(fileBytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(partContentType);
            form.Add(part, "file", fileName);
        }

        request.Content = form;
        return await client.SendAsync(request, TestCancellation.Ct);
    }

    public static async Task<T?> ReadJsonAsync<T>(
        this HttpResponseMessage response,
        CancellationToken ct = default
    )
    {
        return await response.Content.ReadFromJsonAsync<T>(TestJson.Options, ct);
    }

    public static Task ShouldBeBadRequestAsync(this HttpResponseMessage response, ErrorCode code)
    {
        return ShouldFailAsync(response, HttpStatusCode.BadRequest, code);
    }

    public static Task ShouldBeUnauthorizedAsync(this HttpResponseMessage response, ErrorCode code)
    {
        return ShouldFailAsync(response, HttpStatusCode.Unauthorized, code);
    }

    public static Task ShouldBeForbiddenAsync(this HttpResponseMessage response, ErrorCode code)
    {
        return ShouldFailAsync(response, HttpStatusCode.Forbidden, code);
    }

    public static Task ShouldBeNotFoundAsync(this HttpResponseMessage response, ErrorCode code)
    {
        return ShouldFailAsync(response, HttpStatusCode.NotFound, code);
    }

    public static Task ShouldBeConflictAsync(this HttpResponseMessage response, ErrorCode code)
    {
        return ShouldFailAsync(response, HttpStatusCode.Conflict, code);
    }

    private static async Task ShouldFailAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        ErrorCode code
    )
    {
        response.StatusCode.Should().Be(status);
        var error = await response.ReadJsonAsync<ApiErrorResponse>(TestCancellation.Ct);
        error!.Code.Should().Be(code);
    }
}
