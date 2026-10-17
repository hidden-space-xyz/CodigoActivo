using System.Net;
using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.API.Security;

/// <summary>
/// Origin of the current HTTP request: the caller address the forwarded headers middleware took
/// from the trusted proxy, the same address the rate limits partition by. An IPv4 address mapped
/// into IPv6 is given in its IPv4 form. Outside a request there is none.
/// </summary>
/// <param name="accessor">Accessor of the current HTTP context.</param>
public sealed class HttpRequestOrigin(IHttpContextAccessor accessor) : IRequestOrigin
{
    /// <inheritdoc />
    public IPAddress? ClientAddress =>
        accessor.HttpContext?.Connection.RemoteIpAddress is { } address
            ? address.IsIPv4MappedToIPv6
                ? address.MapToIPv4()
                : address
            : null;
}
