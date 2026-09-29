using CodigoActivo.Application.Accounts;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Reads and replaces the stored disposable email domain list. The list is only ever replaced as a
/// whole, by the background refresher.
/// </summary>
public interface IDisposableEmailDomainStore : IDisposableEmailDomainRepository
{
    /// <summary>
    /// Replaces the stored list with <paramref name="domains"/>, writing only the differences. The
    /// change executes immediately in a transaction of its own, outside any staged unit of work, so
    /// concurrent lookups see either the previous list or the new one, never a mix.
    /// </summary>
    /// <param name="domains">Validated normalized domain names that make up the new list.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public Task ReplaceAsync(IReadOnlySet<string> domains, CancellationToken ct = default);
}
