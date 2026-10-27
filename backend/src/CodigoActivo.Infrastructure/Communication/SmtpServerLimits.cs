using System.Text;
using CodigoActivo.Application.Abstractions.Email;

namespace CodigoActivo.Infrastructure.Communication;

/// <summary>
/// Remembers the largest message the SMTP server said it accepts (the <c>SIZE</c> extension) on the
/// last connection of this process, so the outbox refuses a message the server would reject anyway
/// before an administrator believes it sent. Until a connection has told it, every size is accepted.
/// </summary>
public sealed class SmtpServerLimits
{
    private const int HeaderAllowance = 8 * 1024;
    private const int Base64LineInput = 57;
    private const int Base64LineOutput = 78;

    private long maxMessageSize;

    /// <summary>
    /// Gets the largest message the server accepts, in bytes; <see langword="null"/> while unknown or
    /// when the server sets no limit.
    /// </summary>
    public long? MaxMessageSize =>
        Interlocked.Read(ref maxMessageSize) is > 0 and var size ? size : null;

    /// <summary>
    /// Records the limit the server announced on a new connection.
    /// </summary>
    /// <param name="advertised">Announced limit in bytes; zero when the server announced none.</param>
    public void Record(long advertised)
    {
        Interlocked.Exchange(ref maxMessageSize, Math.Max(advertised, 0));
    }

    /// <summary>
    /// Tells whether the server accepts the batch, estimating its encoded size from above: every part,
    /// text included, counted as base64 with its line breaks, plus room for the headers.
    /// </summary>
    /// <param name="batch">Batch whose message is checked.</param>
    /// <returns>
    /// <see langword="false"/> only when the server announced a limit the estimate passes.
    /// </returns>
    public bool Accepts(EmailBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (MaxMessageSize is not { } limit)
        {
            return true;
        }

        var size =
            HeaderAllowance
            + Encoded(Encoding.UTF8.GetByteCount(batch.Subject))
            + Encoded(Encoding.UTF8.GetByteCount(batch.HtmlBody))
            + Encoded(Encoding.UTF8.GetByteCount(batch.TextBody))
            + (batch.Attachments ?? []).Sum(attachment => Encoded(attachment.Content.Length))
            + (batch.InlineImages ?? []).Sum(image => Encoded(image.Content.Length));
        return size <= limit;
    }

    private static long Encoded(long bytes)
    {
        return (bytes + Base64LineInput - 1) / Base64LineInput * Base64LineOutput;
    }
}
