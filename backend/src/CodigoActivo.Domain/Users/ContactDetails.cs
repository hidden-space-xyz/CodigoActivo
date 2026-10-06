using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// Contact details of an independent account: the email it logs in with, a phone and an optional
/// second phone that differs from the first.
/// </summary>
/// <param name="Email">Email of the account.</param>
/// <param name="Phone">Phone of the account.</param>
/// <param name="SecondaryPhone">Second phone, or <see langword="null"/> when there is none.</param>
internal sealed record ContactDetails(
    EmailAddress Email,
    PhoneNumber Phone,
    PhoneNumber? SecondaryPhone
)
{
    /// <summary>
    /// Normalizes and checks the contact details a caller supplied.
    /// </summary>
    /// <param name="details">Details as supplied.</param>
    /// <returns>
    /// The contact details, <see cref="DomainErrorCode.UserContactInfoRequired"/> without an email or a
    /// phone, <see cref="DomainErrorCode.UserPhoneInvalid"/> when a phone is not a <see cref="PhoneNumber"/>,
    /// <see cref="DomainErrorCode.SecondaryPhoneSameAsPrimary"/> when both phones are equal, or
    /// <see cref="DomainErrorCode.UserEmailInvalid"/> when the email is malformed.
    /// </returns>
    public static Result<ContactDetails> From(PersonDetails details)
    {
        if (
            details.Email.NormalizeOrNull() is null
            || details.Phone.NormalizeOrNull() is not { } phoneText
        )
        {
            return Error.Validation(DomainErrorCode.UserContactInfoRequired);
        }

        var phone = PhoneNumber.Create(phoneText);
        if (phone.IsFailure)
        {
            return phone.Error!;
        }

        PhoneNumber? secondaryPhone = null;
        if (details.SecondaryPhone.NormalizeOrNull() is { } secondaryText)
        {
            var secondary = PhoneNumber.Create(secondaryText);
            if (secondary.IsFailure)
            {
                return secondary.Error!;
            }

            secondaryPhone = secondary.Value;
        }

        if (secondaryPhone == phone.Value)
        {
            return Error.Validation(DomainErrorCode.SecondaryPhoneSameAsPrimary);
        }

        var email = EmailAddress.Create(details.Email);
        if (email.IsFailure)
        {
            return email.Error!;
        }

        return new ContactDetails(email.Value, phone.Value, secondaryPhone);
    }
}
