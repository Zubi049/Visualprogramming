using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LanArena;

/// <summary>
/// One TCP connection to the other player. Line based protocol (one message per line).
/// Events are always raised on the UI thread.
/// </summary>
public class NetPeer : IDisposable
{
    readonly TcpClient client;
    readonly StreamReader reader;
    readonly StreamWriter writer;
    readonly SynchronizationContext ctx;
    bool disposed;

    public event Action<string> LineReceived;
    public event Action Disconnected;

    public NetPeer(TcpClient c)
    {
        client = c;
        client.NoDelay = true;
        var s = c.GetStream();
        reader = new StreamReader(s, Encoding.UTF8);
        writer = new StreamWriter(s, new UTF8Encoding(false)) { AutoFlush = true };
        ctx = SynchronizationContext.Current ?? new SynchronizationContext();
    }

    public Task<string> ReadLineAsync() => reader.ReadLineAsync();

    public void StartReceiving()
    {
        Task.Run(async () =>
        {
            try
            {
                string l;
                while ((l = await reader.ReadLineAsync()) != null)
                {
                    var line = l;
                    ctx.Post(_ => { if (!disposed) LineReceived?.Invoke(line); }, null);
                }
            }
            catch { }
            ctx.Post(_ => { if (!disposed) Disconnected?.Invoke(); }, null);
        });
    }

    public void Send(string line)
    {
        try { lock (writer) { writer.WriteLine(line); } } catch { }
    }

    public void Dispose()
    {
        disposed = true;
        try { client.Close(); } catch { }
    }
}
