using System.Diagnostics.CodeAnalysis;
using CodigoActivo.Application.Abstractions.Security;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed class FakeSecretProtector : ISecretProtector
{
    public const string Prefix = "protected:";

    public string Protect(string plaintext)
    {
        return Prefix + plaintext;
    }

    public bool TryUnprotect(string protectedValue, [NotNullWhen(true)] out string? plaintext)
    {
        plaintext = protectedValue.StartsWith(Prefix, StringComparison.Ordinal)
            ? protectedValue[Prefix.Length..]
            : null;
        return plaintext is not null;
    }
}
