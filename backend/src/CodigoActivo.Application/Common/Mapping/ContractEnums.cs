using GenderContract = CodigoActivo.Application.Users.Contracts.Gender;
using TwoFactorMethodContract = CodigoActivo.Application.Accounts.Contracts.TwoFactorMethod;

namespace CodigoActivo.Application.Common.Mapping;

/// <summary>
/// Translates between the enumerations of the domain and those clients send and receive, which
/// share names and values so a stored value reads the same on both sides.
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
    /// Gets the gender clients receive for a domain gender.
    /// </summary>
    /// <param name="gender">Domain gender.</param>
    /// <returns>The gender as clients receive it.</returns>
    public static GenderContract ToContract(this Domain.Users.Gender gender)
    {
        return (GenderContract)(int)gender;
    }

    /// <summary>
    /// Gets the second factor clients receive for a domain second factor.
    /// </summary>
    /// <param name="method">Domain second factor.</param>
    /// <returns>The second factor as clients receive it.</returns>
    public static TwoFactorMethodContract ToContract(this Domain.Users.TwoFactorMethod method)
    {
        return (TwoFactorMethodContract)(int)method;
    }
}
