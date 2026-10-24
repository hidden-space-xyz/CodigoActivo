using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Legal;

/// <summary>
/// A new version of a legal policy came into force, its first one included.
/// </summary>
/// <param name="LegalPolicyId">Identifier of the policy.</param>
public sealed record LegalPolicyPublished(LegalPolicyId LegalPolicyId) : IDomainEvent;
