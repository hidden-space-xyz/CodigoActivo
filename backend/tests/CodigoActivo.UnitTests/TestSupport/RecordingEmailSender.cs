using System.Text.RegularExpressions;
using CodigoActivo.Domain.Communication;

namespace CodigoActivo.UnitTests.TestSupport;

public sealed partial class RecordingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public Exception? ThrowOnSend { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (ThrowOnSend is not null)
            throw ThrowOnSend;

        Sent.Add(message);
        return Task.CompletedTask;
    }

    /// <summary>The verification code carried by the last sent email (the `code=` link parameter).</summary>
    public string LastCode()
    {
        var match = CodePattern().Match(Sent[^1].TextBody);
        return !match.Success
            ? throw new InvalidOperationException(
                "The last email does not contain a verification code."
            )
            : match.Groups[1].Value;
    }

    [GeneratedRegex(@"[?&]code=([^\s&]+)")]
    private static partial Regex CodePattern();
}
