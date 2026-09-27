using System.Text.Json.Serialization;

namespace CodigoActivo.Domain.Repositories;

/// <summary>
/// Identifies who asked for an account to be deleted.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AccountDeletionOrigin>))]
public enum AccountDeletionOrigin
{
    /// <summary>
    /// The account holder deleted their own account.
    /// </summary>
    Self = 1,

    /// <summary>
    /// An administrator deleted somebody else's account.
    /// </summary>
    Administrator = 2,

    /// <summary>
    /// A guardian deleted one of their dependents.
    /// </summary>
    Guardian = 3,
}
