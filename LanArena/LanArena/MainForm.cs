using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace LanArena;

/// <summary>Lobby: list of WLANs on the left, host / join a game on the right.</summary>
public class MainForm : Form
{
    const int GamePort = 47778;

    readonly string myId = Guid.NewGuid().ToString("N").Substring(0, 8);
    readonly Dictionary<string, Room> rooms = new();
    string roomsSig = "";

    ListBox lstWlan, lstRooms;
    Label lblNet, lblStatus;
    TextBox txtName, txtIp;
    RadioButton rbTtt, rbChess;
    Button btnHost, btnRefresh, btnWifi, btnJoin, btnJoinIp;

    TcpListener listener;
    Broadcaster broadcaster;
    RoomListener roomListener;
    System.Windows.Forms.Timer tick;
    bool hosting, busy;
    int hostSession;

    public MainForm()
    {
        BuildUi();
        Load += async (_, _) =>
        {
            roomListener = new RoomListener(myId);
            roomListener.RoomSeen += r => { rooms[r.Key] = r; RefreshRooms(); };
            tick = new System.Windows.Forms.Timer { Interval = 1500 };
            tick.Tick += (_, _) => PruneRooms();
            tick.Start();
            await RefreshWlanAsync();
        };
        FormClosing += (_, _) =>
        {
            StopHosting();
            tick?.Stop();
            roomListener?.Dispose();
        };
    }

    // ------------------------------------------------------------ UI
    static void Rows(TableLayoutPanel t, params (SizeType type, float size)[] rows)
    {
        t.RowCount = rows.Length;
        foreach (var r in rows) t.RowStyles.Add(new RowStyle(r.type, r.size));
    }

    static TableLayoutPanel CardPanel()
    {
        var t = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, BackColor = Ui.Card, Padding = new Padding(18),
            Margin = new Padding(8), ColumnCount = 1
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return t;
    }

    static Label Section(string text) => new Label
    {
        Text = text, Dock = DockStyle.Fill, ForeColor = Ui.Muted, Font = Ui.F(9.5f, FontStyle.Bold),
        TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent
    };

    static ListBox MakeList() => new ListBox
    {
        Dock = DockStyle.Fill, BackColor = Ui.Card, ForeColor = Ui.Txt, BorderStyle = BorderStyle.None,
        Font = Ui.F(11), IntegralHeight = false, Margin = new Padding(4)
    };

    void BuildUi()
    {
        Text = "LAN Arena  -  play over your WLAN";
        ClientSize = new Size(1060, 700);
        MinimumSize = new Size(960, 640);
        BackColor = Ui.Bg;
        ForeColor = Ui.Txt;
        Font = Ui.F(10);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ---- header
        var header = new GradientPanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        var title = Ui.Lbl("LAN Arena", 26, Color.White, true);
        title.Location = new Point(28, 10);
        var sub = Ui.Lbl("Chess & Tic Tac Toe with friends on the same Wi-Fi network", 11, Color.FromArgb(235, 255, 255, 255));
        sub.Location = new Point(32, 60);
        header.Controls.Add(title);
        header.Controls.Add(sub);
        root.Controls.Add(header, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(12), Margin = new Padding(0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // ---- left card: WLAN list
        var left = CardPanel();
        Rows(left, (SizeType.Absolute, 36), (SizeType.Absolute, 50), (SizeType.Percent, 100), (SizeType.Absolute, 54));
        left.Controls.Add(Ui.Lbl("Available WLANs", 15, Ui.Txt, true), 0, 0);
        lblNet = new Label { Dock = DockStyle.Fill, ForeColor = Ui.Muted, Font = Ui.F(9), Text = "Scanning...", BackColor = Color.Transparent };
        left.Controls.Add(lblNet, 0, 1);

        lstWlan = MakeList();
        lstWlan.DrawMode = DrawMode.OwnerDrawFixed;
        lstWlan.ItemHeight = 58;
        lstWlan.DrawItem += DrawWlan;
        lstWlan.DoubleClick += async (_, _) => await ConnectWifiAsync();
        left.Controls.Add(lstWlan, 0, 2);

        var lbtn = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        lbtn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        lbtn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnRefresh = Ui.Btn("Refresh", Ui.Card2);
        btnWifi = Ui.Btn("Connect to selected", Ui.Accent);
        btnRefresh.Click += async (_, _) => await RefreshWlanAsync();
        btnWifi.Click += async (_, _) => await ConnectWifiAsync();
        lbtn.Controls.Add(btnRefresh, 0, 0);
        lbtn.Controls.Add(btnWifi, 1, 0);
        left.Controls.Add(lbtn, 0, 3);

        // ---- right card: play
        var right = CardPanel();
        Rows(right,
            (SizeType.Absolute, 36),   // 0 title
            (SizeType.Absolute, 26),   // 1 name label
            (SizeType.Absolute, 40),   // 2 name box
            (SizeType.Absolute, 30),   // 3 game label
            (SizeType.Absolute, 56),   // 4 game choice
            (SizeType.Absolute, 54),   // 5 host
            (SizeType.Absolute, 34),   // 6 rooms label
            (SizeType.Percent, 100),   // 7 rooms list
            (SizeType.Absolute, 48),   // 8 join selected
            (SizeType.Absolute, 48),   // 9 join by ip
            (SizeType.Absolute, 40));  // 10 status

        right.Controls.Add(Ui.Lbl("Play together", 15, Ui.Txt, true), 0, 0);
        right.Controls.Add(Section("YOUR NAME"), 0, 1);
        txtName = Ui.Input();
        txtName.Text = Environment.UserName;
        txtName.MaxLength = 20;
        right.Controls.Add(txtName, 0, 2);
        right.Controls.Add(Section("CHOOSE A GAME"), 0, 3);

        var games = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        games.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        games.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        rbTtt = GameRadio("Tic Tac Toe");
        rbChess = GameRadio("Chess");
        rbTtt.Checked = true;
        games.Controls.Add(rbTtt, 0, 0);
        games.Controls.Add(rbChess, 1, 0);
        right.Controls.Add(games, 0, 4);

        btnHost = Ui.Btn("Host a game", Ui.Accent);
        btnHost.Font = Ui.F(12, FontStyle.Bold);
        btnHost.Click += async (_, _) => await HostToggleAsync();
        right.Controls.Add(btnHost, 0, 5);

        right.Controls.Add(Section("ROOMS FOUND ON YOUR NETWORK"), 0, 6);
        lstRooms = MakeList();
        lstRooms.BackColor = Ui.Card2;
        lstRooms.DoubleClick += async (_, _) => await JoinSelectedAsync();
        right.Controls.Add(lstRooms, 0, 7);

        btnJoin = Ui.Btn("Join selected room", Ui.Accent2);
        btnJoin.ForeColor = Color.FromArgb(10, 25, 30);
        btnJoin.Click += async (_, _) => await JoinSelectedAsync();
        right.Controls.Add(btnJoin, 0, 8);

        var ipRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        ipRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        txtIp = Ui.Input();
        txtIp.PlaceholderText = "or type host IP, e.g. 192.168.1.5";
        btnJoinIp = Ui.Btn("Join by IP", Ui.Card2);
        btnJoinIp.Click += async (_, _) => await JoinByIpAsync();
        ipRow.Controls.Add(txtIp, 0, 0);
        ipRow.Controls.Add(btnJoinIp, 1, 0);
        right.Controls.Add(ipRow, 0, 9);

        lblStatus = new Label
        {
            Dock = DockStyle.Fill, ForeColor = Ui.Accent2, Font = Ui.F(9.5f), TextAlign = ContentAlignment.MiddleLeft,
            Text = "Ready. Host a game, or join a room that appears above.", BackColor = Color.Transparent
        };
        right.Controls.Add(lblStatus, 0, 10);

        body.Controls.Add(left, 0, 0);
        body.Controls.Add(right, 1, 0);
        root.Controls.Add(body, 0, 1);
        Controls.Add(root);
    }

    static RadioButton GameRadio(string text)
    {
        var rb = new RadioButton
        {
            Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter, Text = text, Font = Ui.F(11, FontStyle.Bold),
            BackColor = Ui.Card2, ForeColor = Ui.Txt, Cursor = Cursors.Hand, Margin = new Padding(4),
            UseVisualStyleBackColor = false
        };
        rb.FlatAppearance.BorderSize = 0;
        rb.FlatAppearance.CheckedBackColor = Ui.Accent;
        rb.FlatAppearance.MouseOverBackColor = Ui.Accent;
        return rb;
    }

    void DrawWlan(object sender, DrawItemEventArgs e)
    {
        var g = e.Graphics;
        using (var bg = new SolidBrush(Ui.Card)) g.FillRectangle(bg, e.Bounds);
        if (e.Index < 0) return;
        var w = (WlanInfo)lstWlan.Items[e.Index];
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var r = Rectangle.Inflate(e.Bounds, -2, -3);
        bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? Ui.Accent : Ui.Card2)) Ui.FillRound(g, b, r, 10);

        string sub = (w.Connected ? "CONNECTED  •  " : "") + (w.Auth == "" ? "Unknown security" : w.Auth) + "  •  " + w.Signal + "%";
        using (var f1 = Ui.F(11, FontStyle.Bold))
        using (var f2 = Ui.F(9))
        using (var tb = new SolidBrush(Ui.Txt))
        using (var mb = new SolidBrush(w.Connected ? Ui.Good : (sel ? Color.FromArgb(225, 228, 255) : Ui.Muted)))
        {
            g.DrawString(w.Ssid, f1, tb, r.X + 14, r.Y + 7);
            g.DrawString(sub, f2, mb, r.X + 14, r.Y + 30);
        }

        int bars = w.Signal >= 75 ? 4 : w.Signal >= 50 ? 3 : w.Signal >= 25 ? 2 : 1;
        int baseY = r.Y + r.Height / 2 + 12;
        for (int i = 0; i < 4; i++)
        {
            int h = 7 + i * 6;
            using var br = new SolidBrush(i < bars ? (w.Connected ? Ui.Good : Ui.Accent2) : Color.FromArgb(70, 255, 255, 255));
            g.FillRectangle(br, r.Right - 56 + i * 11, baseY - h, 7, h);
        }
    }

    void SetStatus(string s) => lblStatus.Text = s;

    // ------------------------------------------------------------ WLAN
    async Task RefreshWlanAsync()
    {
        btnRefresh.Enabled = false;
        lblNet.Text = "Scanning for WLANs...";
        var list = await WlanScanner.ScanAsync();

        lstWlan.BeginUpdate();
        lstWlan.Items.Clear();
        foreach (var w in list) lstWlan.Items.Add(w);
        lstWlan.EndUpdate();

        var cur = list.FirstOrDefault(x => x.Connected);
        var ips = Discovery.LocalIPs();
        lblNet.Text = $"Connected: {(cur != null ? cur.Ssid : "-")}    |    Your IP: {(ips.Count > 0 ? string.Join(", ", ips) : "-")}";
        if (list.Count == 0)
            lblNet.Text += "\nNo Wi-Fi networks found (Wi-Fi off, no adapter, or Windows Location is off).";
        btnRefresh.Enabled = true;
    }

    async Task ConnectWifiAsync()
    {
        if (lstWlan.SelectedItem is not WlanInfo w) { SetStatus("Select a WLAN from the list first."); return; }
        SetStatus($"Connecting to {w.Ssid}...");
        string res = await WlanScanner.ConnectAsync(w.Ssid);
        MessageBox.Show(this, res == "" ? "No response from Windows." : res +
            "\n\n(Windows can only auto-connect to networks you've already saved. Otherwise connect once from the Windows Wi-Fi menu.)",
            "Wi-Fi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await Task.Delay(2500);
        await RefreshWlanAsync();
        SetStatus("Ready.");
    }

    // ------------------------------------------------------------ rooms
    void RefreshRooms()
    {
        string sig = string.Join(",", rooms.Keys.OrderBy(k => k));
        if (sig == roomsSig) return;
        roomsSig = sig;
        string selKey = (lstRooms.SelectedItem as Room)?.Key;
        lstRooms.BeginUpdate();
        lstRooms.Items.Clear();
        foreach (var r in rooms.Values.OrderBy(q => q.Host))
        {
            int i = lstRooms.Items.Add(r);
            if (r.Key == selKey) lstRooms.SelectedIndex = i;
        }
        lstRooms.EndUpdate();
    }

    void PruneRooms()
    {
        var old = rooms.Where(kv => (DateTime.Now - kv.Value.Seen).TotalSeconds > 4).Select(kv => kv.Key).ToList();
        foreach (var k in old) rooms.Remove(k);
        RefreshRooms();
    }

    // ------------------------------------------------------------ hosting
    string PlayerName()
    {
        string n = txtName.Text.Replace("|", "").Trim();
        return n == "" ? Environment.UserName : n;
    }

    string GameKey() => rbChess.Checked ? "Chess" : "TicTacToe";

    static async Task<string> ReadWithTimeout(NetPeer p, int ms)
    {
        var t = p.ReadLineAsync();
        return await Task.WhenAny(t, Task.Delay(ms)) == t ? await t : null;
    }

    void StopHosting()
    {
        hosting = false;
        try { listener?.Stop(); } catch { }
        listener = null;
        broadcaster?.Dispose();
        broadcaster = null;
        btnHost.Text = "Host a game";
        btnHost.BackColor = Ui.Accent;
        btnJoin.Enabled = btnJoinIp.Enabled = true;
    }

    async Task HostToggleAsync()
    {
        if (busy) return;
        if (hosting) { StopHosting(); SetStatus("Hosting cancelled."); return; }

        string name = PlayerName(), game = GameKey();
        try
        {
            listener = new TcpListener(IPAddress.Any, GamePort);
            listener.Start();
        }
        catch (Exception ex)
        {
            listener = null;
            MessageBox.Show(this, "Could not start hosting:\n" + ex.Message, "LAN Arena", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int session = ++hostSession;
        var l = listener;
        hosting = true;
        broadcaster = new Broadcaster(myId, name, game, GamePort);
        btnHost.Text = "Cancel hosting";
        btnHost.BackColor = Ui.Danger;
        btnJoin.Enabled = btnJoinIp.Enabled = false;
        var ips = Discovery.LocalIPs();
        SetStatus($"Hosting {Discovery.Title(game)}  ({(ips.Count > 0 ? ips[0] : "?")}) - waiting for an opponent...");

        NetPeer peer = null;
        string opp = null;
        try
        {
            while (hosting && session == hostSession)
            {
                var c = await l.AcceptTcpClientAsync();
                var p = new NetPeer(c);
                string hello = await ReadWithTimeout(p, 4000);
                if (hello != null && hello.StartsWith("HELLO|")) { peer = p; opp = hello.Substring(6); break; }
                p.Dispose();
            }
        }
        catch { /* listener was stopped */ }

        if (session != hostSession) { peer?.Dispose(); return; }
        StopHosting();
        if (peer == null) return;

        bool hostFirst = Random.Shared.Next(2) == 0;
        peer.Send($"START|{name}|{game}|{(hostFirst ? 1 : 0)}");
        Play(peer, game, name, opp, hostFirst);
    }

    // ------------------------------------------------------------ joining
    Task JoinSelectedAsync()
    {
        if (lstRooms.SelectedItem is Room r) return JoinAsync(r.Ip, r.Port);
        SetStatus("Select a room first (or use Join by IP).");
        return Task.CompletedTask;
    }

    Task JoinByIpAsync()
    {
        string t = txtIp.Text.Trim();
        if (t == "") { SetStatus("Type the host's IP address first."); return Task.CompletedTask; }
        int port = GamePort;
        int i = t.LastIndexOf(':');
        if (i > 0 && int.TryParse(t.Substring(i + 1), out int pp)) { port = pp; t = t.Substring(0, i); }
        return JoinAsync(t, port);
    }

    async Task JoinAsync(string ip, int port)
    {
        if (busy || hosting) return;
        busy = true;
        try
        {
            string name = PlayerName();
            SetStatus($"Connecting to {ip}...");
            var client = new TcpClient();
            try
            {
                var connect = client.ConnectAsync(ip, port);
                if (await Task.WhenAny(connect, Task.Delay(4000)) != connect) throw new TimeoutException("Connection timed out.");
                await connect;
            }
            catch (Exception ex)
            {
                client.Close();
                SetStatus("Could not connect: " + ex.Message);
                return;
            }

            var peer = new NetPeer(client);
            peer.Send("HELLO|" + name);
            string start = await ReadWithTimeout(peer, 5000);
            if (start == null || !start.StartsWith("START|"))
            {
                peer.Dispose();
                SetStatus("The host did not answer. Is it still waiting?");
                return;
            }
            var p = start.Split('|');
            if (p.Length < 4) { peer.Dispose(); SetStatus("Bad reply from host."); return; }
            Play(peer, p[2], name, p[1], p[3] != "1");
        }
        finally { busy = false; }
    }

    void Play(NetPeer peer, string gameKey, string me, string opp, bool first)
    {
        var type = gameKey == "Chess" ? GameType.Chess : GameType.TicTacToe;
        SetStatus($"Playing {Discovery.Title(gameKey)} with {opp}...");
        using var f = new GameForm(peer, type, me, opp, first);
        f.ShowDialog(this);
        SetStatus("Game finished. Host a new game or join a room.");
    }
}
