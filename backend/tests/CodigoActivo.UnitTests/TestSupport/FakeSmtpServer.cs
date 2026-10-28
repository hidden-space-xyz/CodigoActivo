using System.Net;
using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Minimal in-process SMTP server on a loopback port. It speaks plain ESMTP with AUTH PLAIN, records
/// delivered messages and the connections opened, and can announce a size limit, answer MAIL FROM with
/// a refusal, reject a recipient, drop the connection or run a callback on DATA.
/// </summary>
public sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly Lock gate = new();
    private readonly List<ReceivedEmail> received = [];
    private readonly List<Task> sessions = [];
    private readonly Task acceptLoop;
    private int dataCommands;
    private int connections;

    public FakeSmtpServer()
    {
        listener.Start();
        acceptLoop = AcceptAsync();
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>Number of connections the server accepted.</summary>
    public int Connections => Volatile.Read(ref connections);

    /// <summary>Size limit announced with the SIZE extension, if any.</summary>
    public long? AdvertisedSize { get; init; }

    /// <summary>Reply to MAIL FROM instead of accepting the sender, such as a refusal.</summary>
    public string? MailReply { get; init; }

    /// <summary>Recipient answered with a permanent 550 failure.</summary>
    public string? RejectedRecipient { get; init; }

    /// <summary>Reply to the RCPT TO of <see cref="RejectedRecipient"/>, a 5.1.1 refusal by default.</summary>
    public string? RejectedRecipientReply { get; init; }

    /// <summary>Recipient answered with a temporary 450 failure, as greylisting does.</summary>
    public string? DeferredRecipient { get; init; }

    /// <summary>Reply to the DATA command instead of inviting the content.</summary>
    public string? DataCommandReply { get; init; }

    /// <summary>Reply to the message content instead of queueing it, such as a refusal.</summary>
    public string? MessageReply { get; init; }

    /// <summary>Closes the socket without a word once this many messages were queued.</summary>
    public int? CloseAfterQueuedMessages { get; init; }

    /// <summary>Closes the socket without replying once this many DATA commands were received.</summary>
    public int? DropConnectionOnDataCommand { get; init; }

    /// <summary>Runs after a message body is read and before the server replies.</summary>
    public Action? OnMessageData { get; init; }

    public string? AuthenticatedUser { get; private set; }

    public string? AuthenticatedPassword { get; private set; }

    public IReadOnlyList<ReceivedEmail> Messages
    {
        get
        {
            lock (gate)
            {
                return [.. received];
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        listener.Stop();
        await acceptLoop;

        Task[] pending;
        lock (gate)
        {
            pending = [.. sessions];
        }

        await Task.WhenAll(pending);
        listener.Dispose();
        stopping.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (await AcceptUnlessStoppedAsync() is { } client)
        {
            Interlocked.Increment(ref connections);
            lock (gate)
            {
                sessions.Add(HandleSessionAsync(client));
            }
        }
    }

    private async Task<TcpClient?> AcceptUnlessStoppedAsync()
    {
        try
        {
            return await listener.AcceptTcpClientAsync(stopping.Token);
        }
        catch (Exception ex)
            when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            return null;
        }
    }

    private async Task HandleSessionAsync(TcpClient client)
    {
        using (client)
        {
            await RunSessionAsync(client.GetStream(), stopping.Token)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task RunSessionAsync(NetworkStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.Latin1, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.Latin1, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true,
        };

        string from = string.Empty;
        var recipients = new List<string>();
        await writer.WriteLineAsync("220 localhost ESMTP fake");

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            var verb = (line.Length > 4 ? line[..4] : line).ToUpperInvariant();
            switch (verb)
            {
                case "EHLO":
                    var size = AdvertisedSize is { } limit ? $"250-SIZE {limit}\r\n" : string.Empty;
                    await writer.WriteAsync(
                        $"250-localhost\r\n{size}250-AUTH PLAIN\r\n250 8BITMIME\r\n"
                    );
                    break;
                case "AUTH":
                    await AuthenticateAsync(line, reader, writer, ct);
                    break;
                case "MAIL":
                    from = ParsePath(line);
                    await writer.WriteLineAsync(MailReply ?? "250 2.1.0 Ok");
                    break;
                case "RCPT":
                    var recipient = ParsePath(line);
                    if (string.Equals(recipient, RejectedRecipient, StringComparison.Ordinal))
                    {
                        await writer.WriteLineAsync(
                            RejectedRecipientReply
                                ?? $"550 5.1.1 <{recipient}>: Mailbox unavailable"
                        );
                    }
                    else if (string.Equals(recipient, DeferredRecipient, StringComparison.Ordinal))
                    {
                        await writer.WriteLineAsync($"450 4.2.0 <{recipient}>: Greylisted");
                    }
                    else
                    {
                        recipients.Add(recipient);
                        await writer.WriteLineAsync("250 2.1.5 Ok");
                    }

                    break;
                case "DATA":
                    if (DataCommandReply is { } dataRefusal)
                    {
                        recipients = [];
                        await writer.WriteLineAsync(dataRefusal);
                        break;
                    }

                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    var data = await ReadDataAsync(reader, ct);
                    var count = Interlocked.Increment(ref dataCommands);
                    OnMessageData?.Invoke();
                    if (count == DropConnectionOnDataCommand)
                    {
                        return;
                    }

                    if (MessageReply is null)
                    {
                        Record(from, recipients, data);
                    }

                    recipients = [];
                    await writer.WriteLineAsync(MessageReply ?? "250 2.0.0 Queued");
                    if (count == CloseAfterQueuedMessages)
                    {
                        return;
                    }

                    break;
                case "RSET":
                    recipients = [];
                    await writer.WriteLineAsync("250 2.0.0 Ok");
                    break;
                case "QUIT":
                    await writer.WriteLineAsync("221 2.0.0 Bye");
                    return;
                default:
                    await writer.WriteLineAsync("250 2.0.0 Ok");
                    break;
            }
        }
    }

    private async Task AuthenticateAsync(
        string line,
        StreamReader reader,
        StreamWriter writer,
        CancellationToken ct
    )
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? response = parts.Length > 2 ? parts[2] : null;
        if (response is null)
        {
            await writer.WriteLineAsync("334 ");
            response = await reader.ReadLineAsync(ct);
        }

        var credentials = Encoding
            .UTF8.GetString(Convert.FromBase64String(response ?? string.Empty))
            .Split('\0');
        AuthenticatedUser = credentials.ElementAtOrDefault(1);
        AuthenticatedPassword = credentials.ElementAtOrDefault(2);
        await writer.WriteLineAsync("235 2.7.0 Authentication successful");
    }

    private static async Task<string> ReadDataAsync(StreamReader reader, CancellationToken ct)
    {
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(ct) is { } line && line != ".")
        {
            data.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line);
            data.Append("\r\n");
        }

        return data.ToString();
    }

    private void Record(string from, List<string> recipients, string data)
    {
        using var content = new MemoryStream(Encoding.Latin1.GetBytes(data));
        var message = MimeMessage.Load(content);
        lock (gate)
        {
            received.Add(new ReceivedEmail(from, recipients, message));
        }
    }

    private static string ParsePath(string line)
    {
        var start = line.IndexOf('<', StringComparison.Ordinal);
        var end = line.IndexOf('>', StringComparison.Ordinal);
        return start >= 0 && end > start ? line[(start + 1)..end] : string.Empty;
    }
}

/// <summary>Message accepted by <see cref="FakeSmtpServer"/>.</summary>
public sealed record ReceivedEmail(
    string From,
    IReadOnlyList<string> Recipients,
    MimeMessage Message
);
