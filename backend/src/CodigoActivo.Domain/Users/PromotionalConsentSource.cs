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

    /// <summary>
    /// The unsubscribe button the mail client of the holder shows for a promotional email, which
    /// posts the address of its <c>List-Unsubscribe</c> header (RFC 8058).
    /// </summary>
    OneClickUnsubscribe,
}
