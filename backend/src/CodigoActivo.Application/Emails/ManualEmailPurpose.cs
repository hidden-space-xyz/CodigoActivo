namespace CodigoActivo.Application.Emails;

/// <summary>
/// What an administrator writes a manual email for, which decides who may receive it.
/// </summary>
public enum ManualEmailPurpose
{
    /// <summary>
    /// Information the recipients need about their account, their signups or the activities they
    /// take part in. It reaches everyone selected.
    /// </summary>
    Informational,

    /// <summary>
    /// News and promotion of the association, sent on the consent of each recipient (article 6.1.a
    /// GDPR). It reaches only those who gave their promotional consent and carries the contact
    /// address and an unsubscribe link.
    /// </summary>
    Promotional,
}
