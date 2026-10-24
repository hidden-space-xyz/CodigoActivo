using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Legal;

/// <summary>
/// Stores and loads the legal policies.
/// </summary>
public interface ILegalPolicyRepository : IRepository<LegalPolicy>
{
    /// <summary>
    /// Locks the policy of a kind until the running transaction ends and loads it, so revisions of
    /// the same policy run one after another and each one keeps, as a previous version, the text
    /// the one before it published.
    /// </summary>
    /// <param name="kind">Which policy to load.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result is the policy, or <see langword="null"/> when none was published yet.</returns>
    public Task<LegalPolicy?> LockAsync(LegalPolicyKind kind, CancellationToken ct = default);
}
