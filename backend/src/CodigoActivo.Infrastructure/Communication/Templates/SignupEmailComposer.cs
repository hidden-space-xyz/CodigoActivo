using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Abstractions.Time;
using CodigoActivo.Application.Activities;
using CodigoActivo.Application.Common;

namespace CodigoActivo.Infrastructure.Communication.Templates;

/// <summary>
/// Renders the signup decision messages with the site layout, linking to the event page.
/// </summary>
/// <param name="application">Options that carry the public base URL of the site.</param>
/// <param name="clock">Clock whose time zone presents the activity schedule.</param>
public sealed class SignupEmailComposer(ApplicationOptions application, IClock clock)
    : ISignupEmailComposer
{
    private const string EventPath = "/events";

    private string SiteUrl => application.BaseUrl.TrimEnd('/');

    /// <inheritdoc />
    public EmailMessage Confirmed(
        EmailRecipient recipient,
        string? participantName,
        string? roleName,
        SignupActivity activity
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ActivitySignupDecisionEmail.Confirmed(
            recipient.Address,
            recipient.Name,
            participantName,
            roleName,
            Details(activity),
            clock.TimeZone,
            SiteUrl
        );
    }

    /// <inheritdoc />
    public EmailMessage Denied(
        EmailRecipient recipient,
        string? participantName,
        SignupActivity activity
    )
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return ActivitySignupDecisionEmail.Denied(
            recipient.Address,
            recipient.Name,
            participantName,
            Details(activity),
            clock.TimeZone,
            SiteUrl
        );
    }

    private ActivityEmailDetails Details(SignupActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return new ActivityEmailDetails(
            activity.ActivityTitle,
            activity.EventTitle,
            activity.Location,
            activity.StartsAt,
            activity.EndsAt,
            $"{SiteUrl}{EventPath}/{activity.EventId}"
        );
    }
}
