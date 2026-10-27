namespace CodigoActivo.Application.Abstractions.Email;

/// <summary>
/// Names the topics of <see cref="EmailMessage.Topic"/>. A topic says which pending message a new one
/// makes useless: the codes of an account share one, since a new code invalidates the one before;
/// an alert, a signup decision or a coming-of-age notice replaces the pending one about the same
/// thing; the notices to the legal contact use theirs to send one summary at a time. Topics carry
/// identifiers and enum names only, never personal data.
/// </summary>
public static class EmailTopics
{
    /// <summary>
    /// Longest topic stored.
    /// </summary>
    public const int MaxLength = 100;

    /// <summary>
    /// Topic of the codes that confirm a login, an account deletion or a consultation of the blocked
    /// data, which share one stored code per account.
    /// </summary>
    public const string AccountCode = "account-code";

    /// <summary>
    /// Topic of the code that verifies the email of a new account.
    /// </summary>
    public const string AccountVerification = "account-verification";

    /// <summary>
    /// Topic of the link that confirms a new email address.
    /// </summary>
    public const string EmailChange = "email-change";

    /// <summary>
    /// Topic of the notice that someone tried to use an address that already has an account.
    /// </summary>
    public const string EmailInUse = "email-in-use";

    /// <summary>
    /// Topic of the code that resets a password.
    /// </summary>
    public const string PasswordReset = "password-reset";

    /// <summary>
    /// Topic of the summary of withdrawn consents for the legal contact.
    /// </summary>
    public const string ConsentWithdrawals = "consent-withdrawals";

    /// <summary>
    /// Topic of the summary of opened consultations of the blocked data for the legal contact.
    /// </summary>
    public const string BlockedDataConsultations = "blocked-data-consultations";

    /// <summary>
    /// Gets the topic of a security alert about one kind of change.
    /// </summary>
    /// <param name="change">Kind of change the alert reports.</param>
    /// <returns>The topic.</returns>
    public static string SecurityAlert(Enum change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return $"security-alert:{change}";
    }

    /// <summary>
    /// Gets the topic of the decision on one signup to an activity.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the person signed up.</param>
    /// <returns>The topic.</returns>
    public static string SignupDecision(Guid activityId, Guid userId)
    {
        return $"signup-decision:{activityId:N}:{userId:N}";
    }

    /// <summary>
    /// Gets the topic of the notice that a dependent comes of age.
    /// </summary>
    /// <param name="dependentId">Identifier of the dependent.</param>
    /// <returns>The topic.</returns>
    public static string ComingOfAge(Guid dependentId)
    {
        return $"coming-of-age:{dependentId:N}";
    }
}
