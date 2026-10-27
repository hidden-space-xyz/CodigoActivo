using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Security;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Errors;
using CodigoActivo.Application.Common.Security;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Events;
using CodigoActivo.Domain.TermsDecisions;
using CodigoActivo.Domain.TermsDocuments;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.Application.TermsDecisions.Commands;

/// <summary>
/// Carries the input required to withdraw a consent given about a terms document of an event.
/// </summary>
/// <param name="EventId">Identifier of the event.</param>
/// <param name="UserId">Identifier of the person the consent is about.</param>
/// <param name="TermsDocumentId">Identifier of the terms document, which must hold a consent.</param>
public sealed record WithdrawTermsConsentCommand(
    EventId EventId,
    UserId UserId,
    TermsDocumentId TermsDocumentId
) : ICommand<Result>;

/// <summary>
/// Executes the command to withdraw a consent. The person withdraws their own, a guardian those of
/// their dependents, and an administrator records the withdrawal someone asked for by email. The
/// withdrawal is recorded as decided by whoever decides the consent of the person
/// (<see cref="TermsConsent.DeciderOf"/>) and as recorded by the signed-in user; the acceptance it
/// ends stays in the history of the decision. Once it is committed,
/// <see cref="ConsentWithdrawalNotification"/> tells the association's legal contact.
/// </summary>
/// <param name="termsAcceptances">Repository of the terms decisions taken.</param>
/// <param name="termsDocuments">Repository that tells whether the document holds a consent.</param>
/// <param name="users">Repository used to persist and retrieve users.</param>
/// <param name="actingUser">Policy that decides for whom the signed-in user may act.</param>
/// <param name="currentUser">Person the use case runs for.</param>
/// <param name="clock">Clock used to obtain consistent application timestamps.</param>
public sealed class WithdrawTermsConsentCommandHandler(
    IEventTermsAcceptanceRepository termsAcceptances,
    ITermsDocumentRepository termsDocuments,
    IUserRepository users,
    ActingUserPolicy actingUser,
    ICurrentUser currentUser,
    IClock clock
) : ICommandHandler<WithdrawTermsConsentCommand, Result>
{
    /// <summary>
    /// Handles the request to withdraw a consent.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success;
    /// <see cref="ApplicationErrorCode.ActingForAnotherUserForbidden"/> when the user may not act
    /// for the person; <see cref="ApplicationErrorCode.UserNotFound"/>,
    /// <see cref="ApplicationErrorCode.TermsDocumentNotFound"/> or
    /// <see cref="ApplicationErrorCode.EventTermsDecisionNotFound"/> when one is missing;
    /// <see cref="DomainErrorCode.EventTermsWithdrawalNotAllowed"/> when the document is not a
    /// consent, or <see cref="DomainErrorCode.EventTermsConsentNotGiven"/> when it is not accepted
    /// now.
    /// </returns>
    public async Task<Result> HandleAsync(
        WithdrawTermsConsentCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var allowed = await actingUser.EnsureMayActForAsync(command.UserId, ct);
        if (allowed.IsFailure)
        {
            return allowed;
        }

        var person = await users.GetByIdAsync(command.UserId, ct);
        if (person is null)
        {
            return Error.NotFound(ApplicationErrorCode.UserNotFound);
        }

        var document = await termsDocuments.GetByIdAsync(command.TermsDocumentId, ct);
        if (document is null)
        {
            return Error.NotFound(ApplicationErrorCode.TermsDocumentNotFound);
        }

        if (document.Kind is not TermsDocumentKind.Consent)
        {
            return Error.Validation(DomainErrorCode.EventTermsWithdrawalNotAllowed);
        }

        var decision = await termsAcceptances.GetAsync(
            command.EventId,
            command.UserId,
            command.TermsDocumentId,
            ct
        );
        if (decision is null)
        {
            return Error.NotFound(ApplicationErrorCode.EventTermsDecisionNotFound);
        }

        return decision.Withdraw(
            TermsConsent.DeciderOf(person, consent: true, clock.Today),
            currentUser.RequiredId(),
            clock.UtcNow
        );
    }
}
