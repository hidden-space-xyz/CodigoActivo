using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CodigoActivo.UnitTests.Infrastructure.Communication;

internal sealed class ScriptedSmtpServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stopping = new();
    private readonly List<Task> sessions = [];
    private readonly Task accepting;
    private int connections;
    private int delivered;

    public ScriptedSmtpServer()
    {
        listener.Start();
        accepting = AcceptAsync();
    }

    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public int Connections => Volatile.Read(ref connections);

    public int Delivered => Volatile.Read(ref delivered);

    public string AuthReply { get; init; } = "235 2.7.0 Authentication successful";

    public string NoOpReply { get; init; } = "250 2.0.0 Ok";

    public async ValueTask DisposeAsync()
    {
        await stopping.CancelAsync();
        listener.Stop();
        await accepting;
        Task[] pending;
        lock (sessions)
        {
            pending = [.. sessions];
        }

        await Task.WhenAll(pending);
        listener.Dispose();
        stopping.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(stopping.Token);
            }
            catch (Exception ex)
                when (ex is OperationCanceledException or SocketException or ObjectDisposedException
                )
            {
                return;
            }

            Interlocked.Increment(ref connections);
            lock (sessions)
            {
                sessions.Add(ServeAsync(client));
            }
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            await RunAsync(client.GetStream(), stopping.Token)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task RunAsync(NetworkStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.Latin1, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.Latin1, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true,
        };
        await writer.WriteLineAsync("220 localhost ESMTP scripted");

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            switch ((line.Length > 4 ? line[..4] : line).ToUpperInvariant())
            {
                case "EHLO":
                    await writer.WriteAsync("250-localhost\r\n250-AUTH PLAIN\r\n250 8BITMIME\r\n");
                    break;
                case "AUTH":
                    await writer.WriteLineAsync(AuthReply);
                    break;
                case "NOOP":
                    await writer.WriteLineAsync(NoOpReply);
                    break;
                case "DATA":
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    while (await reader.ReadLineAsync(ct) is { } data && data != ".") { }

                    Interlocked.Increment(ref delivered);
                    await writer.WriteLineAsync("250 2.0.0 Queued");
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
}
