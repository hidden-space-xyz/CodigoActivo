namespace CodigoActivo.Application.Emails;

/// <summary>
/// What an administrator writes a manual email for, which decides who may receive it.
/// </summary>
public enum ManualEmailPurpose
{
    /// <summary>
    /// Something that concerns the recipients: their account, their signups or an activity they
    /// already take part in. It reaches everyone selected.
    /// </summary>
    Notice,

    /// <summary>
    /// Upcoming events, activities and news of the association, sent on the consent of each
    /// recipient (article 6.1.a GDPR). It reaches only those who gave their news consent and
    /// carries the contact address and an unsubscribe link.
    /// </summary>
    News,
}
