namespace CodigoActivo.Domain.Users;

/// <summary>
/// Stage of a person's account.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// Registered account that has not confirmed its email yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Verified account that can sign in.
    /// </summary>
    Active,

    /// <summary>
    /// Dependent of a guardian, which never signs in by itself.
    /// </summary>
    Dependent,
}
