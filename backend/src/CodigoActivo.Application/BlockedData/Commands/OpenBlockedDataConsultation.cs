using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Persistence;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Accounts.Passwords;
using CodigoActivo.Application.Accounts.SecondFactor;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.BlockedData;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.BlockedData.Commands;

/// <summary>
/// Carries the input required to open a consultation of the blocked data.
/// </summary>
/// <param name="Authority">Who the data is made available to.</param>
/// <param name="Reference">Reference of the request, such as the number of the proceedings.</param>
/// <param name="CurrentPassword">Password of the signed-in user, re-entered to authorize it.</param>
/// <param name="Code">Code of the authenticator app.</param>
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
/// password and a code of its authenticator app, whose wrong codes count towards the same lockout
/// as logins; the opening is announced to the legal contact.
/// </summary>
/// <param name="officer">Policy that tells who may consult the blocked data.</param>
/// <param name="users">Repository used to lock the account while its code is checked.</param>
/// <param name="consultations">Repository of the consultations.</param>
/// <param name="passwordAttempts">Guard that verifies, counts and locks account passwords.</param>
/// <param name="secondFactor">Guard that verifies, counts and locks second-factor codes.</param>
/// <param name="uow">Unit of work used to commit the code check.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class OpenBlockedDataConsultationCommandHandler(
    PrivacyOfficer officer,
    IUserRepository users,
    IBlockedDataConsultationRepository consultations,
    PasswordAttemptGuard passwordAttempts,
    SecondFactorGuard secondFactor,
    IUnitOfWork uow,
    IClock clock
) : ICommandHandler<OpenBlockedDataConsultationCommand, Result<BlockedDataConsultationId>>
{
    /// <summary>
    /// Handles the request to open a consultation of the blocked data.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task whose result carries the identifier of the consultation, or the error.</returns>
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

        var verified = await uow.ExecuteInTransactionAsync(
            attempt => VerifyCodeLockedAsync(user, command.Code, attempt),
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

    private async Task<Result> VerifyCodeLockedAsync(User user, string code, CancellationToken ct)
    {
        if (!await users.LockAsync(user, ct))
        {
            return Error.Forbidden(ApplicationErrorCode.BlockedDataOfficerOnly);
        }

        return await secondFactor.VerifyLockedAsync(user, code, clock.UtcNow, ct);
    }
}
