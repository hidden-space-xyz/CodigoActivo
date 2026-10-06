using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Partners;

/// <summary>
/// Stores and loads partners.
/// </summary>
public interface IPartnerRepository : IRepository<Partner>
{
    /// <summary>
    /// Loads a partner to change it.
    /// </summary>
    /// <param name="id">Identifier of the partner.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the partner, or <see langword="null"/> when it does not exist.</returns>
    public Task<Partner?> GetByIdAsync(PartnerId id, CancellationToken ct = default);
}
