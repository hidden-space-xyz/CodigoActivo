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
        using var response = await client.GetAsync(TestUri.Rel("/api/auth/csrf"), ct);
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
        {
            request.Content = JsonContent.Create(
                body,
                body.GetType(),
                mediaType: null,
                TestJson.Options
            );
        }

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

        using var form = new MultipartFormDataContent();
        using var filePart =
            fileBytes is null ? null : CreateBinaryPart(fileBytes, partContentType);
        if (filePart is not null)
        {
            form.Add(filePart, "file", fileName);
        }

        request.Content = form;
        return await client.SendAsync(request, TestCancellation.Ct);
    }

    public static async Task<HttpResponseMessage> SendEmailFormAsync(
        this HttpClient client,
        string url,
        string? subject = "Asunto de prueba",
        string? body = "Cuerpo de prueba",
        IReadOnlyList<(string FileName, string ContentType, byte[] Bytes)>? attachments = null,
        bool withCsrf = true
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (withCsrf)
        {
            var token = await client.FetchCsrfTokenAsync(TestCancellation.Ct);
            request.Headers.Add("X-CSRF-TOKEN", token);
        }

        using var form = new MultipartFormDataContent();
        using var subjectPart = subject is null ? null : new StringContent(subject);
        if (subjectPart is not null)
        {
            form.Add(subjectPart, "subject");
        }

        using var bodyPart = body is null ? null : new StringContent(body);
        if (bodyPart is not null)
        {
            form.Add(bodyPart, "body");
        }

        return await AddAttachmentThenSendAsync(client, request, form, attachments ?? [], 0);
    }

    private static async Task<HttpResponseMessage> AddAttachmentThenSendAsync(
        HttpClient client,
        HttpRequestMessage request,
        MultipartFormDataContent form,
        IReadOnlyList<(string FileName, string ContentType, byte[] Bytes)> attachments,
        int index
    )
    {
        if (index >= attachments.Count)
        {
            request.Content = form;
            return await client.SendAsync(request, TestCancellation.Ct);
        }

        var (fileName, contentType, bytes) = attachments[index];
        using var part = CreateBinaryPart(bytes, contentType);
        form.Add(part, "attachments", fileName);
        return await AddAttachmentThenSendAsync(client, request, form, attachments, index + 1);
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

    private static ByteArrayContent CreateBinaryPart(byte[] bytes, string contentType)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return part;
    }
}
