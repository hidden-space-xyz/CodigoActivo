namespace CodigoActivo.Application.Accounts;

/// <summary>
/// Reads the stored disposable email domain list, the last one that passed validation. The list
/// is empty until a first list is obtained.
/// </summary>
public interface IDisposableEmailDomainRepository
{
    /// <summary>
    /// Determines whether any of the supplied normalized domains is on the stored list.
    /// </summary>
    /// <param name="domains">Normalized domain names to look up.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is <see langword="true"/> when at least one domain is listed.</returns>
    public Task<bool> ContainsAnyAsync(
        IReadOnlyCollection<string> domains,
        CancellationToken ct = default
    );
}
