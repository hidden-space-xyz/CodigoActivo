using System.Buffers.Text;
using System.Security.Cryptography;
using CodigoActivo.Application.Emails;
using CodigoActivo.Domain.Users;
using Microsoft.AspNetCore.DataProtection;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Protects the account identifier of an unsubscribe link with the ASP.NET Core Data Protection key
/// ring, which encrypts and authenticates it, so a link names an account only when the API issued
/// it. A token never expires while the key that issued it stays in the ring; recreating the ring
/// invalidates the links already sent.
/// </summary>
public sealed class DataProtectionUnsubscribeTokens : IUnsubscribeTokens
{
    private const string Purpose = "CodigoActivo.Unsubscribe.v1";

    private const int GuidLength = 16;

    private readonly IDataProtector protector;

    /// <summary>
    /// Initializes the protector for the unsubscribe purpose.
    /// </summary>
    /// <param name="provider">Data protection provider configured by the host.</param>
    public DataProtectionUnsubscribeTokens(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        protector = provider.CreateProtector(Purpose);
    }

    /// <inheritdoc />
    public string Issue(UserId userId)
    {
        return Base64Url.EncodeToString(protector.Protect(userId.Value.ToByteArray()));
    }

    /// <inheritdoc />
    public bool TryRead(string token, out UserId userId)
    {
        ArgumentNullException.ThrowIfNull(token);
        userId = default;
        if (!Base64Url.IsValid(token))
        {
            return false;
        }

        byte[] payload;
        try
        {
            payload = protector.Unprotect(Base64Url.DecodeFromChars(token));
        }
        catch (CryptographicException)
        {
            return false;
        }

        if (payload.Length != GuidLength)
        {
            return false;
        }

        userId = UserId.From(new Guid(payload));
        return true;
    }
}
