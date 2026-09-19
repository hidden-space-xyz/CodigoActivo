using System.Text.RegularExpressions;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed partial class FakeEmailSender : IEmailTransport
{
    private readonly List<EmailMessage> sent = [];
    private readonly HashSet<string> failingRecipients = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (sent)
            {
                return [.. sent];
            }
        }
    }

    public Exception? ThrowOnSend { get; set; }

    public void FailFor(params string[] addresses)
    {
        lock (sent)
        {
            failingRecipients.UnionWith(addresses);
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        lock (sent)
        {
            if (failingRecipients.Contains(message.ToAddress))
            {
                throw new InvalidOperationException(
                    "The fake SMTP server rejected a configured recipient."
                );
            }

            sent.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (sent)
        {
            sent.Clear();
            failingRecipients.Clear();
            ThrowOnSend = null;
        }
    }

    public string LastOtpSentTo(string address)
    {
        EmailMessage? message;
        lock (sent)
        {
            message = sent.LastOrDefault(m =>
                string.Equals(m.ToAddress, address, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (message is null)
        {
            throw new InvalidOperationException($"No email was sent to '{address}'.");
        }

        var match = OtpPattern.Match(message.TextBody);
        return !match.Success
            ? throw new InvalidOperationException(
                $"The email sent to '{address}' does not contain a verification code."
            )
            : match.Groups["code"].Value;
    }

    /// <summary>
    /// Drops the recorded login-code emails, so tests that count application mail after signing in
    /// only see the messages their scenario produced.
    /// </summary>
    public void ForgetLoginCodes()
    {
        lock (sent)
        {
            sent.RemoveAll(m => m.Kind is EmailKind.TwoFactorCode);
        }
    }

    public string LastLoginCodeSentTo(string address)
    {
        EmailMessage? message;
        lock (sent)
        {
            message = sent.LastOrDefault(m =>
                m.Kind is EmailKind.TwoFactorCode
                && string.Equals(m.ToAddress, address, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (message is null)
        {
            throw new InvalidOperationException($"No login code was sent to '{address}'.");
        }

        var match = LoginCodePattern.Match(message.TextBody);
        return !match.Success
            ? throw new InvalidOperationException(
                $"The login code email sent to '{address}' does not contain a code."
            )
            : match.Groups["code"].Value;
    }

    [GeneratedRegex(
        @"[?&]code=(?<code>[^\s&]+)",
        RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex OtpPattern { get; }

    [GeneratedRegex(
        @"^(?<code>\d{6})$",
        RegexOptions.ExplicitCapture | RegexOptions.Multiline,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex LoginCodePattern { get; }
}
