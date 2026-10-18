namespace CodigoActivo.Domain.Users;

/// <summary>
/// Where the holder of an account gave or withdrew the promotional consent.
/// </summary>
public enum PromotionalConsentSource
{
    /// <summary>
    /// The registration form.
    /// </summary>
    Registration,

    /// <summary>
    /// The profile the holder edits in their account.
    /// </summary>
    Profile,

    /// <summary>
    /// The unsubscribe link of a promotional email.
    /// </summary>
    UnsubscribeLink,
}
