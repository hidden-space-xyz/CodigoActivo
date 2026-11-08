using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Legal;

/// <summary>
/// Stores and loads the legal policies.
/// </summary>
public interface ILegalPolicyRepository : IRepository<LegalPolicy>
{
    /// <summary>
    /// Loads the policy of a kind to revise it.
    /// </summary>
    /// <param name="kind">Which policy to load.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result is the policy, or <see langword="null"/> when none was published yet.
    /// </returns>
    public Task<LegalPolicy?> GetByKindAsync(LegalPolicyKind kind, CancellationToken ct = default);
}
