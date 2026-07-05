using System.Text.Json;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.Domain.Common;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.API.Middlewares;

public sealed class GlobalExceptionHandlerTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static readonly GlobalExceptionHandler Sut = new(NullLogger<GlobalExceptionHandler>.Instance);

    private static DefaultHttpContext NewContext(string traceId = "trace-123") =>
        new() { Response = { Body = new MemoryStream() }, TraceIdentifier = traceId };

    private static async Task<ApiErrorResponse> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(context.Response.Body, WebJson);
        return body!;
    }

    [Fact]
    public async Task TryHandleAsync_returns_true_to_mark_exception_handled()
    {
        var context = NewContext();

        var handled = await Sut.TryHandleAsync(context, new InvalidOperationException("boom"), default);

        handled.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsync_sets_status_500()
    {
        var context = NewContext();

        await Sut.TryHandleAsync(context, new InvalidOperationException(), default);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsync_writes_unexpected_error_body_with_trace_identifier()
    {
        var context = NewContext("abc-999");

        await Sut.TryHandleAsync(context, new InvalidOperationException("secret detail"), default);

        var body = await ReadBodyAsync(context);
        body.Code.Should().Be(ErrorCode.UnexpectedError);
        body.Status.Should().Be(StatusCodes.Status500InternalServerError);
        body.Title.Should().Be("Internal Server Error");
        body.TraceId.Should().Be("abc-999");
    }
}
