using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.API.Extensions;
using CodigoActivo.Domain.Common;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodigoActivo.UnitTests.API.Extensions;

public sealed class HttpContextExtensionsTests
{
    [Fact]
    public async Task WriteApiErrorAsync_sets_status_and_writes_json_body()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-http-1" };
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        var error = Error.NotFound(ErrorCode.PartnerNotFound);

        await context.WriteApiErrorAsync(error);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        responseBody.Position = 0;
        var json = Encoding.UTF8.GetString(responseBody.ToArray());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status404NotFound);
        root.GetProperty("title").GetString().Should().Be("Not Found");
        root.GetProperty("code").GetString().Should().Be(nameof(ErrorCode.PartnerNotFound));
        root.GetProperty("traceId").GetString().Should().Be("trace-http-1");
    }

    [Fact]
    public async Task WriteApiErrorAsync_maps_bad_request_kind()
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-http-2" };
        context.Response.Body = new MemoryStream();
        var error = Error.BadRequest(ErrorCode.PartnerThumbnailNotFound);

        await context.WriteApiErrorAsync(error);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
