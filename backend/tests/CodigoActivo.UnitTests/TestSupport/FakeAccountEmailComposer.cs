using CodigoActivo.Application.Abstractions.Email;
using CodigoActivo.Application.Accounts.Notifications;
using CodigoActivo.Domain.Users;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed record ComposedAccountEmail(string Name, EmailRecipient Recipient)
{
    public UserId? UserId { get; init; }

    public string? Code { get; init; }

    public TimeSpan? Lifetime { get; init; }

    public bool? NewsConsent { get; init; }

    public AccountSecurityChange? Change { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }

    public string? MaskedNewEmail { get; init; }
}

public sealed class FakeAccountEmailComposer : IAccountEmailComposer
{
    private readonly List<ComposedAccountEmail> composed = [];

    public IReadOnlyList<ComposedAccountEmail> Composed
    {
        get
        {
            lock (composed)
            {
                return [.. composed];
            }
        }
    }

    public EmailMessage AccountVerification(
        EmailRecipient recipient,
        UserId userId,
        string code,
        TimeSpan lifetime,
        bool newsConsent
    )
    {
        return Compose(
            EmailKind.AccountVerification,
            new(nameof(AccountVerification), recipient)
            {
                UserId = userId,
                Code = code,
                Lifetime = lifetime,
                NewsConsent = newsConsent,
            }
        );
    }

    public EmailMessage EmailChangeConfirmation(
        EmailRecipient recipient,
        UserId userId,
        string code,
        TimeSpan lifetime
    )
    {
        return Compose(
            EmailKind.AccountVerification,
            new(nameof(EmailChangeConfirmation), recipient)
            {
                UserId = userId,
                Code = code,
                Lifetime = lifetime,
            }
        );
    }

    public EmailMessage EmailInUse(EmailRecipient recipient)
    {
        return Compose(EmailKind.AccountVerification, new(nameof(EmailInUse), recipient));
    }

    public EmailMessage PasswordReset(
        EmailRecipient recipient,
        UserId userId,
        string code,
        TimeSpan lifetime
    )
    {
        return Compose(
            EmailKind.PasswordReset,
            new(nameof(PasswordReset), recipient)
            {
                UserId = userId,
                Code = code,
                Lifetime = lifetime,
            }
        );
    }

    public EmailMessage LoginCode(EmailRecipient recipient, string code, TimeSpan lifetime)
    {
        return Compose(
            EmailKind.TwoFactorCode,
            new(nameof(LoginCode), recipient) { Code = code, Lifetime = lifetime }
        );
    }

    public EmailMessage AccountDeletionCode(
        EmailRecipient recipient,
        string code,
        TimeSpan lifetime
    )
    {
        return Compose(
            EmailKind.TwoFactorCode,
            new(nameof(AccountDeletionCode), recipient) { Code = code, Lifetime = lifetime }
        );
    }

    public EmailMessage SecurityAlert(
        EmailRecipient recipient,
        AccountSecurityChange change,
        DateTimeOffset occurredAt,
        string? maskedNewEmail
    )
    {
        return Compose(
            EmailKind.SecurityAlert,
            new(nameof(SecurityAlert), recipient)
            {
                Change = change,
                OccurredAt = occurredAt,
                MaskedNewEmail = maskedNewEmail,
            }
        );
    }

    private EmailMessage Compose(EmailKind kind, ComposedAccountEmail email)
    {
        lock (composed)
        {
            composed.Add(email);
        }

        var body = email.ToString();
        return new EmailMessage(
            kind,
            email.Recipient.Address,
            email.Recipient.Name,
            email.Name,
            body,
            body
        );
    }
}
