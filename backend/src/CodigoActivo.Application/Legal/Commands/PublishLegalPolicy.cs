using System.ComponentModel.DataAnnotations;
using CodigoActivo.Application.Abstractions.Auditing;
using CodigoActivo.Application.Abstractions.Messaging;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Common.Validation;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Legal;

namespace CodigoActivo.Application.Legal.Commands;

/// <summary>
/// Carries the input required to publish a new version of a legal policy.
/// </summary>
/// <param name="Kind">Which policy to publish.</param>
/// <param name="Content">Rich-text document holding text only; images are refused.</param>
public sealed record PublishLegalPolicyCommand(
    LegalPolicyKind Kind,
    [property: Required, RichText(AllowImages = false), MaxLength(RichTextAllowlist.MaxJsonLength)]
        string Content
) : ICommand<Result>, IRoutineCommand;

/// <summary>
/// Executes the command to publish a new version of a legal policy: the first publication creates
/// the policy and later ones revise it, keeping the version they replace; the same content as the
/// version in force publishes nothing new.
/// </summary>
/// <param name="legalPolicies">Repository used to persist and retrieve legal policies.</param>
/// <param name="clock">Clock used to date the new version.</param>
/// <param name="auditSubject">Names the published policy in the audit log.</param>
public sealed class PublishLegalPolicyCommandHandler(
    ILegalPolicyRepository legalPolicies,
    IClock clock,
    IAuditSubject auditSubject
) : ICommandHandler<PublishLegalPolicyCommand, Result>
{
    /// <summary>
    /// Handles the request to publish a new version of a legal policy.
    /// </summary>
    /// <param name="command">Command containing the operation input.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>
    /// A task whose result reports success, or
    /// <see cref="DomainErrorCode.LegalPolicyContentRequired"/> when the content is empty.
    /// </returns>
    public async Task<Result> HandleAsync(
        PublishLegalPolicyCommand command,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var content = RichText.From(command.Content);
        var policy = await legalPolicies.GetByKindAsync(command.Kind, ct);
        if (policy is null)
        {
            var published = LegalPolicy.Publish(command.Kind, content, clock.UtcNow);
            if (published.IsFailure)
            {
                return published.Error!;
            }

            policy = published.Value;
            await legalPolicies.AddAsync(policy, ct);
        }
        else
        {
            var revised = policy.Revise(content, clock.UtcNow);
            if (revised.IsFailure)
            {
                return revised;
            }
        }

        auditSubject.Name(policy.Id);
        return Result.Success();
    }
}
