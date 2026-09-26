using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Repositories;

namespace CodigoActivo.Application.Auth;

/// <summary>
/// Tells whether an email address belongs to a disposable (temporary) mailbox provider, so
/// registration and email changes can refuse it. An address matches when its domain or any parent
/// domain is on the stored list, which a background worker keeps up to date. While no list has ever
/// been stored nothing matches, so every address is accepted.
/// </summary>
/// <param name="domains">Repository holding the last valid disposable email domain list.</param>
public sealed class DisposableEmailChecker(IDisposableEmailDomainRepository domains)
{
    /// <summary>
    /// Determines whether the address uses a disposable email domain.
    /// </summary>
    /// <param name="email">Normalized email address to check.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when the address must be refused.</returns>
    public async Task<bool> IsDisposableAsync(string email, CancellationToken ct = default)
    {
        var names = EmailDomains.LookupNames(email);
        return names.Count > 0 && await domains.ContainsAnyAsync(names, ct);
    }
}
