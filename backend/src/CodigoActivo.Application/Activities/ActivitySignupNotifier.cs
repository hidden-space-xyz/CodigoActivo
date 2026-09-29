using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Querying;
using CodigoActivo.Application.Common.Diagnostics;
using CodigoActivo.Domain.Activities;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Activities;

/// <summary>
/// Builds and sends notifications for activity signup.
/// </summary>
/// <param name="readStore">Read side used to describe the activity and reach the participant.</param>
/// <param name="executor">Query executor used to materialize database results.</param>
/// <param name="emailSender">The email sender value.</param>
/// <param name="composer">Composer that renders the decision messages.</param>
/// <param name="logger">Logger used to record operational diagnostics.</param>
public sealed class ActivitySignupNotifier(
    IReadStore readStore,
    IQueryExecutor executor,
    IEmailSender emailSender,
    ISignupEmailComposer composer,
    ILogger<ActivitySignupNotifier> logger
)
{
    /// <summary>
    /// Notifies the affected users about decision.
    /// </summary>
    /// <param name="activityId">Identifier of the activity.</param>
    /// <param name="userId">Identifier of the user.</param>
    /// <param name="statusId">Identifier of the status.</param>
    /// <param name="roleTypeId">Identifier of the role type.</param>
    /// <param name="ct">Cancellation token used to stop the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task NotifyDecisionAsync(
        Guid activityId,
        Guid userId,
        Guid statusId,
        Guid roleTypeId,
        CancellationToken ct
    )
    {
        try
        {
            var activity = await GetSignupActivityAsync(activityId, ct);
            if (activity is null)
            {
                return;
            }

            var contacts = await GetContactsAsync([userId], ct);
            if (
                !contacts.TryGetValue(userId, out var contact)
                || ResolveRecipient(contact) is not { } recipient
            )
            {
                return;
            }

            var participantName = recipient.IsGuardian ? contact.FullName : null;
            var message = AssignmentDecisions.IsConfirmation(statusId)
                ? composer.Confirmed(
                    new EmailRecipient(recipient.Address, recipient.Name),
                    participantName,
                    (await GetRoleNamesAsync(ct)).GetValueOrDefault(roleTypeId),
                    activity
                )
                : composer.Denied(
                    new EmailRecipient(recipient.Address, recipient.Name),
                    participantName,
                    activity
                );

            await emailSender.SendAsync(message, ct);
        }
        catch (EmailRateLimitedException)
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.EmailSendFailed(EmailKind.ActivityNotification, ex);
        }
    }

    private async Task<SignupActivity?> GetSignupActivityAsync(
        Guid activityId,
        CancellationToken ct
    )
    {
        return await executor.FirstOrDefaultAsync(
            readStore
                .Activities.Where(a => a.Id == activityId)
                .Select(a => new SignupActivity(
                    a.Title,
                    a.Event.Title,
                    a.EventId,
                    a.Location,
                    a.ActivityStartsAt,
                    a.ActivityEndsAt
                )),
            ct
        );
    }

    private async Task<Dictionary<Guid, UserContact>> GetContactsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct
    )
    {
        var contacts = await executor.ToListAsync(
            readStore
                .Users.Where(u => userIds.Contains(u.Id))
                .Select(u => new UserContact(
                    u.Id,
                    u.FirstName,
                    u.LastName,
                    u.Email,
                    u.Parent == null ? null : u.Parent.FirstName,
                    u.Parent == null ? null : u.Parent.Email
                )),
            ct
        );

        return contacts.ToDictionary(contact => contact.Id);
    }

    private async Task<Dictionary<Guid, string>> GetRoleNamesAsync(CancellationToken ct)
    {
        var roles = await executor.ToListAsync(
            readStore.ActivityRoleTypes.Select(role => new { role.Id, role.Name }),
            ct
        );
        return roles.ToDictionary(role => role.Id, role => role.Name);
    }

    private static NotificationRecipient? ResolveRecipient(UserContact contact)
    {
        return contact switch
        {
            { Email: { } email } when !string.IsNullOrWhiteSpace(email) =>
                new NotificationRecipient(email, contact.FirstName, IsGuardian: false),
            { GuardianEmail: { } guardianEmail } when !string.IsNullOrWhiteSpace(guardianEmail) =>
                new NotificationRecipient(
                    guardianEmail,
                    contact.GuardianFirstName ?? string.Empty,
                    IsGuardian: true
                ),
            _ => null,
        };
    }

    private sealed record UserContact(
        Guid Id,
        string FirstName,
        string LastName,
        string? Email,
        string? GuardianFirstName,
        string? GuardianEmail
    )
    {
        /// <summary>
        /// Gets the full name value.
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    private sealed record NotificationRecipient(string Address, string Name, bool IsGuardian);
}
