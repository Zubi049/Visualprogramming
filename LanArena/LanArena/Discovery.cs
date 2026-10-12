using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace LanArena;

public class Room
{
    public string Id, Host, Game, Ip;
    public int Port;
    public DateTime Seen;
    public string Key => Ip + ":" + Port;
    public override string ToString() => $"{Host}   •   {Discovery.Title(Game)}   •   {Ip}";
}

public static class Discovery
{
    public const int UdpPort = 47777;
    public const string Magic = "LANARENA1";

    public static string Title(string game) => game == "Chess" ? "Chess" : "Tic Tac Toe";

    public static List<string> LocalIPs()
    {
        var l = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) l.Add(ua.Address.ToString());
            }
        }
        catch { }
        return l;
    }

    internal static List<IPEndPoint> BroadcastTargets()
    {
        var list = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, UdpPort) };
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                    byte[] ip = ua.Address.GetAddressBytes(), mask = ua.IPv4Mask.GetAddressBytes(), b = new byte[4];
                    for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                    list.Add(new IPEndPoint(new IPAddress(b), UdpPort));
                }
            }
        }
        catch { }
        return list;
    }
}

/// <summary>Host side: shouts "I have a room!" on the LAN once per second.</summary>
public class Broadcaster : IDisposable
{
    readonly CancellationTokenSource cts = new();

    public Broadcaster(string id, string host, string game, int tcpPort)
    {
        var token = cts.Token;
        Task.Run(async () =>
        {
            try
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;
                byte[] data = Encoding.UTF8.GetBytes($"{Discovery.Magic}|{id}|{host}|{game}|{tcpPort}");
                while (!token.IsCancellationRequested)
                {
                    foreach (var ep in Discovery.BroadcastTargets())
                    {
                        try { await udp.SendAsync(data, data.Length, ep); } catch { }
                    }
                    await Task.Delay(1000, token);
                }
            }
            catch { }
        });
    }

    public void Dispose() => cts.Cancel();
}

/// <summary>Client side: listens for rooms. Raises RoomSeen on the UI thread (create it on the UI thread).</summary>
public class RoomListener : IDisposable
{
    UdpClient udp;
    readonly string myId;
    public event Action<Room> RoomSeen;

    public RoomListener(string myId)
    {
        this.myId = myId;
        try
        {
            udp = new UdpClient(AddressFamily.InterNetwork);
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, Discovery.UdpPort));
            Loop();
        }
        catch { udp = null; }
    }

    async void Loop()
    {
        while (true)
        {
            UdpReceiveResult r;
            try { r = await udp.ReceiveAsync(); } catch { return; }
            var p = Encoding.UTF8.GetString(r.Buffer).Split('|');
            if (p.Length < 5 || p[0] != Discovery.Magic || p[1] == myId) continue;
            if (!int.TryParse(p[4], out int port)) continue;
            RoomSeen?.Invoke(new Room { Id = p[1], Host = p[2], Game = p[3], Port = port, Ip = r.RemoteEndPoint.Address.ToString(), Seen = DateTime.Now });
        }
    }

    public void Dispose() { try { udp?.Close(); } catch { } }
}
