using System.Net;
using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace CodigoActivo.UnitTests.TestSupport;

/// <summary>
/// Minimal in-process SMTP server on a loopback port. It speaks plain ESMTP with AUTH PLAIN, records
/// delivered messages and can reject a recipient, drop the connection or run a callback on DATA.
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

    public FakeSmtpServer()
    {
        listener.Start();
        acceptLoop = AcceptAsync();
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>Recipient answered with a permanent 550 failure.</summary>
    public string? RejectedRecipient { get; init; }

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
        try
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync(stopping.Token);
                lock (gate)
                {
                    sessions.Add(HandleSessionAsync(client));
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException)
        {
            // The server is stopping.
        }
    }

    private async Task HandleSessionAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                await RunSessionAsync(client.GetStream(), stopping.Token);
            }
        }
        catch (Exception ex)
            when (ex is IOException or OperationCanceledException or ObjectDisposedException)
        {
            // The client or the server closed the connection.
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
                    await writer.WriteAsync("250-localhost\r\n250-AUTH PLAIN\r\n250 8BITMIME\r\n");
                    break;
                case "AUTH":
                    await AuthenticateAsync(line, reader, writer, ct);
                    break;
                case "MAIL":
                    from = ParsePath(line);
                    await writer.WriteLineAsync("250 2.1.0 Ok");
                    break;
                case "RCPT":
                    var recipient = ParsePath(line);
                    if (string.Equals(recipient, RejectedRecipient, StringComparison.Ordinal))
                    {
                        await writer.WriteLineAsync("550 5.1.1 Mailbox unavailable");
                    }
                    else
                    {
                        recipients.Add(recipient);
                        await writer.WriteLineAsync("250 2.1.5 Ok");
                    }

                    break;
                case "DATA":
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    var data = await ReadDataAsync(reader, ct);
                    var count = Interlocked.Increment(ref dataCommands);
                    OnMessageData?.Invoke();
                    if (count == DropConnectionOnDataCommand)
                    {
                        return;
                    }

                    Record(from, recipients, data);
                    recipients = [];
                    await writer.WriteLineAsync("250 2.0.0 Queued");
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

        var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(response ?? string.Empty))
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
public sealed record ReceivedEmail(string From, IReadOnlyList<string> Recipients, MimeMessage Message);
