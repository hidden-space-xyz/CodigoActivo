using System.Text.Json;
using AwesomeAssertions;
using CodigoActivo.API.Contracts;
using CodigoActivo.API.Middlewares;
using CodigoActivo.Domain.Common;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CodigoActivo.UnitTests.API.Middlewares;

public sealed class GlobalExceptionHandlerTests
{
    private const string RealPath = "/api/users/8f7c2b1e-0000-4000-8000-000000000000";
    private const string RouteTemplate = "api/users/{id}";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static readonly GlobalExceptionHandler Sut = new(
        NullLogger<GlobalExceptionHandler>.Instance
    );

    private static DefaultHttpContext NewContext(string traceId = "trace-123")
    {
        return new() { Response = { Body = new MemoryStream() }, TraceIdentifier = traceId };
    }

    private static RouteEndpoint UserEndpoint()
    {
        return new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(RouteTemplate),
            order: 0,
            new EndpointMetadataCollection(),
            "Users"
        );
    }

    private static async Task<RecordingLogger<GlobalExceptionHandler>> HandleAsync(
        Action<DefaultHttpContext> arrange
    )
    {
        var logger = new RecordingLogger<GlobalExceptionHandler>();
        var context = NewContext();
        context.Request.Method = "DELETE";
        context.Request.Path = RealPath;
        context.Request.QueryString = new QueryString("?email=ana@test.com");
        arrange(context);

        await new GlobalExceptionHandler(logger).TryHandleAsync(
            context,
            new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken
        );

        return logger;
    }

    private static async Task<ApiErrorResponse> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(
            context.Response.Body,
            WebJson,
            TestContext.Current.CancellationToken
        );
        return body!;
    }

    [Fact]
    public async Task TryHandleAsyncAnyExceptionReturnsTrue()
    {
        var context = NewContext();

        var handled = await Sut.TryHandleAsync(
            context,
            new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken
        );

        handled.Should().BeTrue();
    }

    [Fact]
    public async Task TryHandleAsyncAnyExceptionSetsStatus500()
    {
        var context = NewContext();

        await Sut.TryHandleAsync(
            context,
            new InvalidOperationException(),
            TestContext.Current.CancellationToken
        );

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task TryHandleAsyncAnyExceptionWritesUnexpectedErrorBodyWithTraceId()
    {
        var context = NewContext("abc-999");

        await Sut.TryHandleAsync(
            context,
            new InvalidOperationException("secret detail"),
            TestContext.Current.CancellationToken
        );

        var body = await ReadBodyAsync(context);
        body.Code.Should().Be(ErrorCode.UnexpectedError);
        body.Status.Should().Be(StatusCodes.Status500InternalServerError);
        body.Title.Should().Be("Internal Server Error");
        body.TraceId.Should().Be("abc-999");
    }

    [Fact]
    public async Task TryHandleAsyncMatchedEndpointLogsTheMethodAndTheRouteTemplate()
    {
        var logger = await HandleAsync(context => context.SetEndpoint(UserEndpoint()));

        var entry = logger.LevelEntries.Should().ContainSingle().Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry
            .Message.Should()
            .StartWith($"Unhandled exception while processing DELETE {RouteTemplate}")
            .And.NotContain(RealPath)
            .And.NotContain("8f7c2b1e-0000-4000-8000-000000000000")
            .And.NotContain("ana@test.com");
    }

    [Fact]
    public async Task TryHandleAsyncClearedEndpointStillLogsTheRouteTemplateFromTheFeature()
    {
        var logger = await HandleAsync(context =>
            context.Features.Set<IExceptionHandlerFeature>(
                new ExceptionHandlerFeature
                {
                    Endpoint = UserEndpoint(),
                    Path = RealPath,
                    Error = new InvalidOperationException("boom"),
                }
            )
        );

        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith($"Unhandled exception while processing DELETE {RouteTemplate}")
            .And.NotContain(RealPath);
    }

    [Fact]
    public async Task TryHandleAsyncWithoutAnEndpointLogsAFixedLiteralInsteadOfThePath()
    {
        var logger = await HandleAsync(_ => { });

        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("Unhandled exception while processing DELETE (no endpoint)")
            .And.NotContain(RealPath);
    }
}
