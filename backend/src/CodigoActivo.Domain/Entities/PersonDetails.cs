namespace CodigoActivo.Domain.Entities;

/// <summary>
/// Details of a person exactly as a caller supplied them, before normalization. The kind of
/// account decides which of them apply: an independent account uses the contact details, the DNI
/// or NIE and the promotional consent and has no birth date, while a dependent uses only the birth
/// date and ignores the rest.
/// </summary>
/// <param name="FirstName">Given name; stored trimmed.</param>
/// <param name="LastName">Family name; stored trimmed.</param>
/// <param name="Gender">The gender value.</param>
/// <param name="Email">Login email of an independent account; stored trimmed and lowercase.</param>
/// <param name="Phone">Contact phone of an independent account; stored trimmed.</param>
/// <param name="SecondaryPhone">
/// Optional second phone of an independent account; blank means none and it must differ from
/// <paramref name="Phone"/>.
/// </param>
/// <param name="NationalId">DNI or NIE of an independent account, in any spacing or casing.</param>
/// <param name="PromotionalConsent">
/// Whether an independent account agrees to receive promotional content.
/// </param>
/// <param name="BirthDate">Birth date of a dependent; an independent account has none.</param>
public sealed record PersonDetails(
    string FirstName,
    string LastName,
    Gender Gender,
    string? Email = null,
    string? Phone = null,
    string? SecondaryPhone = null,
    string? NationalId = null,
    bool PromotionalConsent = false,
    DateOnly? BirthDate = null
);
