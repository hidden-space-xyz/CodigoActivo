namespace CodigoActivo.Domain.Users;

/// <summary>
/// Guardian of an erased dependent, as the legal copy keeps them: enough to identify who decided
/// on behalf of the dependent.
/// </summary>
/// <param name="Id">Identifier of the guardian.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="NationalId">DNI or NIE.</param>
/// <param name="Email">Email address.</param>
/// <param name="Phone">Phone number.</param>
public sealed record LegalCopyGuardian(
    Guid Id,
    string FirstName,
    string LastName,
    string? NationalId,
    string? Email,
    string? Phone
);
