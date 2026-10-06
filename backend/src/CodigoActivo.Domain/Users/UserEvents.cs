using CodigoActivo.Domain.Common;

namespace CodigoActivo.Domain.Users;

/// <summary>
/// An independent account was registered.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record AccountRegistered(UserId UserId) : IDomainEvent;

/// <summary>
/// A dependent was added to a household.
/// </summary>
/// <param name="UserId">Identifier of the dependent.</param>
/// <param name="GuardianId">Identifier of the guardian.</param>
public sealed record DependentAdded(UserId UserId, UserId GuardianId) : IDomainEvent;

/// <summary>
/// The profile of an account changed: its personal or contact details, or its pending email.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record ProfileChanged(UserId UserId) : IDomainEvent;

/// <summary>
/// The email or the phones an account is reached at were replaced, so the holder is told at the
/// address the account had.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="PreviousEmail">Email the account had before the change.</param>
/// <param name="FirstName">Given name of the holder.</param>
/// <param name="NewEmail">Email the account has now, when it was replaced.</param>
/// <param name="PhonesReplaced">Whether the phone or the secondary phone were replaced.</param>
public sealed record ContactDetailsReplaced(
    UserId UserId,
    EmailAddress? PreviousEmail,
    string FirstName,
    EmailAddress? NewEmail,
    bool PhonesReplaced
) : IDomainEvent;

/// <summary>
/// The holder of an account changed its password.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record PasswordChanged(UserId UserId) : IDomainEvent;

/// <summary>
/// The password of an account was reset with an emailed code.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record PasswordReset(UserId UserId) : IDomainEvent;

/// <summary>
/// An authenticator application became the second factor of an account.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record AuthenticatorEnabled(UserId UserId) : IDomainEvent;

/// <summary>
/// The holder of an account went back to email as its second factor.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record AuthenticatorDisabled(UserId UserId) : IDomainEvent;

/// <summary>
/// An administrator returned an account that used an authenticator to email as its second factor.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record TwoFactorReset(UserId UserId) : IDomainEvent;

/// <summary>
/// An account was granted or lost the administrator rights.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
/// <param name="IsAdmin">Whether the account is an administrator now.</param>
public sealed record AdministratorRightsChanged(UserId UserId, bool IsAdmin) : IDomainEvent;

/// <summary>
/// The membership type of an account changed.
/// </summary>
/// <param name="UserId">Identifier of the account.</param>
public sealed record UserTypeChanged(UserId UserId) : IDomainEvent;

/// <summary>
/// An account was erased, keeping only the legal copy of its data.
/// </summary>
/// <param name="UserId">Identifier of the erased account.</param>
public sealed record AccountErased(UserId UserId) : IDomainEvent;
