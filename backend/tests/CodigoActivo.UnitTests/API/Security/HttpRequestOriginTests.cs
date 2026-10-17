using System.Net;
using AwesomeAssertions;
using CodigoActivo.API.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodigoActivo.UnitTests.API.Security;

public sealed class HttpRequestOriginTests
{
    private static HttpRequestOrigin Origin(string? address)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);
        return new HttpRequestOrigin(new HttpContextAccessor { HttpContext = context });
    }

    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("::ffff:203.0.113.9", "203.0.113.9")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    public void ClientAddressIsTheForwardedCallerInItsPlainForm(string remote, string expected)
    {
        Origin(remote).ClientAddress.Should().Be(IPAddress.Parse(expected));
    }

    [Fact]
    public void ClientAddressOutsideARequestIsNull()
    {
        new HttpRequestOrigin(new HttpContextAccessor()).ClientAddress.Should().BeNull();
        Origin(null).ClientAddress.Should().BeNull();
    }
}
