using System.Net;

namespace CodigoActivo.Application.Abstractions.Security;

/// <summary>
/// Tells where the request being served comes from.
/// </summary>
public interface IRequestOrigin
{
    /// <summary>
    /// Gets the address of the caller as the trusted proxy forwarded it, or <see langword="null"/>
    /// outside a request.
    /// </summary>
    public IPAddress? ClientAddress { get; }
}
