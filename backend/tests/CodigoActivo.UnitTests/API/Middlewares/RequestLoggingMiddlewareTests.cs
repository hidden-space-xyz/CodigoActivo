using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Middlewares;
using CodigoActivo.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodigoActivo.UnitTests.API.Middlewares;

public sealed class RequestLoggingMiddlewareTests
{
    private readonly RecordingLogger<RequestLoggingMiddleware> logger = new();

    private RequestLoggingMiddleware BuildSut(int statusCode)
    {
        return new(
            context =>
            {
                context.Response.StatusCode = statusCode;
                return Task.CompletedTask;
            },
            logger
        );
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/users";
        context.Request.QueryString = new QueryString("?email=ana%40example.test&phone=600111222");
        context.Request.Headers.Cookie = "__Host-CodigoActivo.Session=secret-ticket";
        context.Request.Headers.Referer = "https://codigoactivo.test/admin/users?search=ana";
        context.Request.Headers["X-CSRF-TOKEN"] = "csrf-secret";
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        return context;
    }

    [Fact]
    public async Task InvokeAsyncFailedRequestLogsMethodPathAndStatus()
    {
        await BuildSut(StatusCodes.Status400BadRequest).InvokeAsync(NewContext());

        logger
            .Entries.Should()
            .ContainSingle()
            .Which.Should()
            .StartWith("HTTP GET /api/users responded 400");
    }

    [Fact]
    public async Task InvokeAsyncFailedRequestLogsNoQueryHeadersOrAddress()
    {
        await BuildSut(StatusCodes.Status500InternalServerError).InvokeAsync(NewContext());

        var entry = logger.Entries.Should().ContainSingle().Subject;
        entry
            .Should()
            .NotContainAny("ana", "600111222", "secret-ticket", "csrf-secret", "203.0.113.7");
    }
}
