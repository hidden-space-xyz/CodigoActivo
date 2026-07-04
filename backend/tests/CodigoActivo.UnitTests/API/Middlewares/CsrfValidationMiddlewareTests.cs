using System.Text.Json;
using CodigoActivo.API.Extensions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CodigoActivo.UnitTests.API.Middlewares;

public sealed class CsrfValidationMiddlewareTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly IAntiforgery antiforgery = Substitute.For<IAntiforgery>();
    private bool nextCalled;

    private CsrfValidationMiddleware BuildSut() =>
        new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            antiforgery,
            NullLogger<CsrfValidationMiddleware>.Instance
        );

    private static DefaultHttpContext NewContext(string method, IAntiforgeryMetadata? metadata = null)
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        context.Request.Method = method;
        if (metadata is not null)
            context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test"));
        return context;
    }

    private static async Task<ApiErrorResponse> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(context.Response.Body, WebJson);
        return body!;
    }

    private sealed record FakeAntiforgeryMetadata(bool RequiresValidation) : IAntiforgeryMetadata;

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("get")]
    public async Task InvokeAsync_skips_validation_for_safe_methods_and_calls_next(string method)
    {
        var context = NewContext(method);

        await BuildSut().InvokeAsync(context);

        nextCalled.Should().BeTrue();
        await antiforgery.DidNotReceive().ValidateRequestAsync(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task InvokeAsync_validates_unsafe_method_and_calls_next_when_token_valid()
    {
        antiforgery.ValidateRequestAsync(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var context = NewContext("POST");

        await BuildSut().InvokeAsync(context);

        await antiforgery.Received(1).ValidateRequestAsync(context);
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task InvokeAsync_writes_400_invalid_csrf_and_short_circuits_when_validation_throws()
    {
        antiforgery
            .ValidateRequestAsync(Arg.Any<HttpContext>())
            .Returns(Task.FromException(new AntiforgeryValidationException("bad token")));
        var context = NewContext("POST");

        await BuildSut().InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var body = await ReadBodyAsync(context);
        body.Code.Should().Be(ErrorCode.InvalidCsrfToken);
        body.Status.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task InvokeAsync_skips_validation_when_endpoint_opts_out()
    {
        var context = NewContext("POST", new FakeAntiforgeryMetadata(RequiresValidation: false));

        await BuildSut().InvokeAsync(context);

        nextCalled.Should().BeTrue();
        await antiforgery.DidNotReceive().ValidateRequestAsync(Arg.Any<HttpContext>());
    }

    [Fact]
    public async Task InvokeAsync_validates_when_endpoint_metadata_requires_validation()
    {
        antiforgery.ValidateRequestAsync(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var context = NewContext("POST", new FakeAntiforgeryMetadata(RequiresValidation: true));

        await BuildSut().InvokeAsync(context);

        await antiforgery.Received(1).ValidateRequestAsync(context);
        nextCalled.Should().BeTrue();
    }
}
