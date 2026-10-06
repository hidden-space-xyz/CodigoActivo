namespace CodigoActivo.Domain.Users;

/// <summary>
/// Validated, normalized change to a person's profile, planned by
/// <see cref="User.PlanProfileChange"/> and applied by <see cref="User.ApplyProfileChange"/>.
/// Nothing changes until it is applied, so the checks that need I/O (a disposable or taken email,
/// the caller's password) can run in between, guided by what the change replaces.
/// </summary>
public sealed class ProfileChange
{
    internal ProfileChange(
        User account,
        PersonDetails details,
        ContactDetails? contact,
        SpanishNationalId? nationalId,
        DateOnly? birthDate
    )
    {
        Account = account;
        FirstName = details.FirstName.Trim();
        LastName = details.LastName.Trim();
        Gender = details.Gender;
        Contact = contact;
        NationalId = nationalId;
        PromotionalConsent = contact is not null && details.PromotionalConsent;
        BirthDate = birthDate;
        NewEmail = contact is not null && contact.Email != account.Email ? contact.Email : null;
        ReplacesPhones =
            contact is not null
            && (contact.Phone != account.Phone || contact.SecondaryPhone != account.SecondaryPhone);
        ReplacesContact = NewEmail is not null || ReplacesPhones;
    }

    /// <summary>
    /// Gets the email the account keeps or receives; <see langword="null"/> for a dependent, which
    /// has none of its own.
    /// </summary>
    public EmailAddress? Email => Contact?.Email;

    /// <summary>
    /// Gets the email when it replaces the stored one; <see langword="null"/> when the email stays
    /// the same or the account is a dependent.
    /// </summary>
    public EmailAddress? NewEmail { get; }

    /// <summary>
    /// Gets whether the change replaces the email, the phone or the secondary phone of the account,
    /// which lets whoever makes it take the account over or redirect its contact details.
    /// </summary>
    public bool ReplacesContact { get; }

    /// <summary>
    /// Gets whether the change replaces the phone or the secondary phone of the account.
    /// </summary>
    public bool ReplacesPhones { get; }

    internal User Account { get; }

    internal string FirstName { get; }

    internal string LastName { get; }

    internal Gender Gender { get; }

    internal ContactDetails? Contact { get; }

    internal SpanishNationalId? NationalId { get; }

    internal bool PromotionalConsent { get; }

    internal DateOnly? BirthDate { get; }
}
