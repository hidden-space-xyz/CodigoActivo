using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the input required to open a consultation of the blocked data.
/// </summary>
/// <param name="Authority">Who the data is made available to.</param>
/// <param name="Reference">Reference of the request, such as the number of the proceedings.</param>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize it.</param>
/// <param name="Code">Code from the consultation email or the authenticator application.</param>
public sealed record OpenBlockedDataConsultationCommand(
    [property: EnumDataType(typeof(BlockedDataAuthority))] BlockedDataAuthority Authority,
    [property:
        Required,
        MaxLength(BlockedDataConsultation.ReferenceMaxLength),
        NotBlank,
        SingleLine
    ]
        string Reference,
    [property: Required, MaxLength(128), NotBlank] string CurrentPassword,
    [property: Required, MaxLength(16), NotBlank] string Code
) : ICommand<Result<BlockedDataConsultationId>>;

/// <summary>
/// Executes the command that opens a consultation of the blocked data for a request of an
/// authority. Only the account designated for data protection opens one, after its current
/// password and a code of its second factor, emailed on demand or read from its authenticator app.
/// The code is checked and spent with the account locked, so it opens a single consultation, and
/// wrong codes count towards the same lockout as logins. The consultation stays open for
/// <see cref="BlockedDataConsultation.Validity"/>, and its opening is announced to the legal
/// contact once it is committed.
/// </summary>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="accountLock">Runs the check of the code with the account locked.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="secondFactor">Guard that verifies, counts and locks second-factor codes.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class OpenBlockedDataConsultationCommandHandler(
    PrivacyOfficer officer,
    AccountLock accountLock,
    IBlockedDataConsultationRepository consultations,
    PasswordAttemptGuard passwordAttempts,
    SecondFactorGuard secondFactor,
    IClock clock
) : ICommandHandler<OpenBlockedDataConsultationCommand, Result<BlockedDataConsultationId>>
{
    /// <summary>
    /// Handles the request to open a consultation of the blocked data.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result carries the identifier of the consultation;
    /// <see cref="ApplicationErrorCode.BlockedDataOfficerOnly"/> for any other account,
    /// <see cref="ApplicationErrorCode.UserCurrentPasswordIncorrect"/>,
    /// <see cref="DomainErrorCode.TwoFactorLocked"/>,
    /// <see cref="ApplicationErrorCode.TwoFactorCodeInvalid"/>, or
    /// <see cref="DomainErrorCode.BlockedDataReferenceRequired"/>.
    /// </returns>
    public async Task<Result<BlockedDataConsultationId>> HandleAsync(
        OpenBlockedDataConsultationCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var signedIn = await officer.SignedInOfficerAsync(ct);
        if (signedIn.IsFailure)
        {
            return signedIn.Error!;
        }

        var user = signedIn.Value;
        if (!await passwordAttempts.VerifyReauthenticationAsync(user, command.CurrentPassword, ct))
        {
            return Error.Validation(ApplicationErrorCode.UserCurrentPasswordIncorrect);
        }

        var verified = await accountLock.RunAsync(
            user,
            attempt => secondFactor.VerifyLockedAsync(user, command.Code, clock.UtcNow, attempt),
            Error.Forbidden(ApplicationErrorCode.BlockedDataOfficerOnly),
            ct
        );
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        var opened = BlockedDataConsultation.Open(
            user.Id,
            command.Authority,
            command.Reference,
            clock.UtcNow
        );
        if (opened.IsFailure)
        {
            return opened.Error!;
        }

        await consultations.AddAsync(opened.Value, ct);
        return opened.Value.Id;
    }
}
