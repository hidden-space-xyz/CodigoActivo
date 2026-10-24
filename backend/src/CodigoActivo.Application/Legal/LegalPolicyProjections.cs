using System.Linq.Expressions;
using CodigoActivo.Application.Abstractions.Querying.ReadModel;
using CodigoActivo.Application.Legal.Contracts;

namespace CodigoActivo.Application.Legal;

/// <summary>
/// Database projections from the read models to the response shapes of the legal policies.
/// </summary>
public static class LegalPolicyProjections
{
    /// <summary>
    /// Projects a policy to the version in force with its content.
    /// </summary>
    public static readonly Expression<Func<LegalPolicyRow, LegalPolicyResponse>> InForce =
        policy => new LegalPolicyResponse
        {
            Kind = policy.Kind,
            Version = policy.Version,
            Content = policy.Content,
            PublishedAt = policy.PublishedAt,
        };

    /// <summary>
    /// Projects a replaced version of a policy with its content.
    /// </summary>
    public static readonly Expression<Func<LegalPolicyVersionRow, LegalPolicyResponse>> Replaced =
        version => new LegalPolicyResponse
        {
            Kind = version.LegalPolicy.Kind,
            Version = version.Number,
            Content = version.Content,
            PublishedAt = version.PublishedAt,
        };
}
