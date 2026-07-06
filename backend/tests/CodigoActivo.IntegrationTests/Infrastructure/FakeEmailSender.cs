using System.Text.RegularExpressions;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.IntegrationTests.Infrastructure;

public sealed partial class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> sent = [];

    public IReadOnlyList<EmailMessage> Sent => sent;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        lock (sent)
        {
            sent.Add(message);
        }

        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (sent)
        {
            sent.Clear();
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
            throw new InvalidOperationException($"No email was sent to '{address}'.");

        // The OTP is the `code=` parameter of the verification link.
        var match = OtpPattern().Match(message.TextBody);
        if (!match.Success)
            throw new InvalidOperationException(
                $"The email sent to '{address}' does not contain a verification code."
            );

        return match.Groups[1].Value;
    }

    [GeneratedRegex(@"[?&]code=([^\s&]+)")]
    private static partial Regex OtpPattern();
}
