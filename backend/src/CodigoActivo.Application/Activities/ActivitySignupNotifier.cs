using CodigoActivo.Application.Activities.Queries;
using CodigoActivo.Application.Emails;
using CodigoActivo.Application.Options;
using CodigoActivo.Domain.Common;
using CodigoActivo.Domain.Communication;
using CodigoActivo.Domain.Constants;
using CodigoActivo.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace CodigoActivo.Application.Activities;

public readonly record struct SignupLine(Guid UserId, Guid RoleTypeId);

public sealed class ActivitySignupNotifier(
    IActivityRepository activities,
    IUserRepository users,
    IQueryExecutor executor,
    IClock clock,
    IEmailSender emailSender,
    ApplicationOptions application,
    ListActivityRoleTypesQueryHandler roleTypesQuery,
    ILogger<ActivitySignupNotifier> logger
)
{
    private const string EventPath = "/events";
    private const string AccountPath = "/account";

    public async Task NotifySignupAsync(
        Guid activityId,
        Guid recipientUserId,
        IReadOnlyList<SignupLine> lines,
        CancellationToken ct
    )
    {
        try
        {
            var details = await GetEmailDetailsAsync(activityId, ct);
            if (details is null)
            {
                return;
            }

            var contacts = await GetContactsAsync(
                [.. lines.Select(line => line.UserId).Append(recipientUserId).Distinct()],
                ct
            );
            if (
                !contacts.TryGetValue(recipientUserId, out var target)
                || ResolveRecipient(target) is not { } recipient
            )
            {
                return;
            }

            var roleNames = await GetRoleNamesAsync(ct);
            var participants = new List<ActivitySignupParticipant>(lines.Count);
            foreach (var line in lines)
            {
                if (contacts.TryGetValue(line.UserId, out var contact))
                {
                    participants.Add(
                        new ActivitySignupParticipant(
                            contact.FullName,
                            roleNames.GetValueOrDefault(line.RoleTypeId, string.Empty)
                        )
                    );
                }
            }

            if (participants.Count is 0)
            {
                return;
            }

            await emailSender.SendAsync(
                ActivitySignupEmail.Create(
                    recipient.Address,
                    recipient.Name,
                    details,
                    participants,
                    clock.TimeZone,
                    BuildUrl(AccountPath),
                    BuildSiteUrl()
                ),
                ct
            );
        }
        catch (EmailRateLimitedException)
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send the signup confirmation email for activity {ActivityId}",
                activityId
            );
        }
    }

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
            var details = await GetEmailDetailsAsync(activityId, ct);
            if (details is null)
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
            var message =
                statusId == SeedIds.AssignmentStatusTypes.Confirmed
                    ? ActivitySignupDecisionEmail.Confirmed(
                        recipient.Address,
                        recipient.Name,
                        participantName,
                        (await GetRoleNamesAsync(ct)).GetValueOrDefault(roleTypeId),
                        details,
                        clock.TimeZone,
                        BuildSiteUrl()
                    )
                    : ActivitySignupDecisionEmail.Denied(
                        recipient.Address,
                        recipient.Name,
                        participantName,
                        details,
                        clock.TimeZone,
                        BuildSiteUrl()
                    );

            await emailSender.SendAsync(message, ct);
        }
        catch (EmailRateLimitedException)
        {
            return;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Failed to send the signup decision email for activity {ActivityId}",
                activityId
            );
        }
    }

    private async Task<ActivityEmailDetails?> GetEmailDetailsAsync(
        Guid activityId,
        CancellationToken ct
    )
    {
        var data = await executor.FirstOrDefaultAsync(
            activities
                .Query()
                .Where(a => a.Id == activityId)
                .Select(a => new ActivityEmailData(
                    a.Title,
                    a.Event.Title,
                    a.EventId,
                    a.Location,
                    a.ActivityStartsAt,
                    a.ActivityEndsAt
                )),
            ct
        );

        return data is null
            ? null
            : new ActivityEmailDetails(
                data.ActivityTitle,
                data.EventTitle,
                data.Location,
                data.StartsAt,
                data.EndsAt,
                BuildUrl($"{EventPath}/{data.EventId}")
            );
    }

    private async Task<Dictionary<Guid, UserContact>> GetContactsAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken ct
    )
    {
        var contacts = await executor.ToListAsync(
            users
                .Query()
                .Where(u => userIds.Contains(u.Id))
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
        var roles = await roleTypesQuery.HandleAsync(new ListActivityRoleTypesQuery(), ct);
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

    private string BuildSiteUrl()
    {
        return application.BaseUrl.TrimEnd('/');
    }

    private string BuildUrl(string path)
    {
        return $"{BuildSiteUrl()}{path}";
    }

    private sealed record ActivityEmailData(
        string ActivityTitle,
        string EventTitle,
        Guid EventId,
        string Location,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt
    );

    private sealed record UserContact(
        Guid Id,
        string FirstName,
        string LastName,
        string? Email,
        string? GuardianFirstName,
        string? GuardianEmail
    )
    {
        public string FullName => $"{FirstName} {LastName}".Trim();
    }

    private sealed record NotificationRecipient(string Address, string Name, bool IsGuardian);
}
