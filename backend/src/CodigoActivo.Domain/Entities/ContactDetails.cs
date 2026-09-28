using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Normalized contact details of an independent account: the email it logs in with, a phone and
/// an optional second phone that differs from the first.
/// </summary>
/// <param name="Email">Trimmed, lowercase email.</param>
/// <param name="Phone">Trimmed phone.</param>
/// <param name="SecondaryPhone">Trimmed second phone, or <see langword="null"/> when there is none.</param>
internal sealed record ContactDetails(string Email, string Phone, string? SecondaryPhone)
{
    /// <summary>
    /// Normalizes and checks the contact details a caller supplied.
    /// </summary>
    /// <param name="details">Details as supplied.</param>
    /// <returns>
    /// The contact details, <see cref="ErrorCode.UserContactInfoRequired"/> without an email or a
    /// phone, or <see cref="ErrorCode.SecondaryPhoneSameAsPrimary"/> when both phones are equal.
    /// </returns>
    public static Result<ContactDetails> From(PersonDetails details)
    {
        var email = details.Email.NormalizeEmailOrNull();
        var phone = details.Phone.NormalizeOrNull();
        if (email is null || phone is null)
        {
            return Error.BadRequest(ErrorCode.UserContactInfoRequired);
        }

        var secondaryPhone = details.SecondaryPhone.NormalizeOrNull();
        if (string.Equals(secondaryPhone, phone, StringComparison.Ordinal))
        {
            return Error.BadRequest(ErrorCode.SecondaryPhoneSameAsPrimary);
        }

        return new ContactDetails(email, phone, secondaryPhone);
    }
}
