using GenderContract = CodigoActivo.Application.Abstractions.Querying.ReadModel.Gender;
using LegalPolicyKindContract = CodigoActivo.Application.Abstractions.Querying.ReadModel.LegalPolicyKind;
using TermsDocumentKindContract = CodigoActivo.Application.Abstractions.Querying.ReadModel.TermsDocumentKind;

namespace CodigoActivo.Application.Common.Mapping;

/// <summary>
/// Translates the enumerations clients send into those of the domain, which share names and values
/// so a stored value reads the same on both sides.
/// </summary>
public static class ContractEnums
{
    /// <summary>
    /// Gets the domain gender of a gender sent by a client.
    /// </summary>
    /// <param name="gender">Gender as clients send it.</param>
    /// <returns>The domain gender.</returns>
    public static Domain.Users.Gender ToDomain(this GenderContract gender)
    {
        return (Domain.Users.Gender)(int)gender;
    }

    /// <summary>
    /// Gets the domain kind of a terms document kind sent by a client.
    /// </summary>
    /// <param name="kind">Kind as clients send it.</param>
    /// <returns>The domain kind.</returns>
    public static Domain.TermsDocuments.TermsDocumentKind ToDomain(
        this TermsDocumentKindContract kind
    )
    {
        return (Domain.TermsDocuments.TermsDocumentKind)(int)kind;
    }

    /// <summary>
    /// Gets the domain kind of a legal policy kind sent by a client.
    /// </summary>
    /// <param name="kind">Kind as clients send it.</param>
    /// <returns>The domain kind.</returns>
    public static Domain.Legal.LegalPolicyKind ToDomain(this LegalPolicyKindContract kind)
    {
        return (Domain.Legal.LegalPolicyKind)(int)kind;
    }
}
