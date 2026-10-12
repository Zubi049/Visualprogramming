using System.Drawing;
using System.Windows.Forms;

namespace LanArena;

public enum GameType { TicTacToe, Chess }

/// <summary>The game window: board on the left, players / status / chat on the right.</summary>
public class GameForm : Form
{
    readonly NetPeer peer;
    readonly GameType type;
    readonly string me, opp;
    bool first, gameOver, iWant, oppWant, connected = true;
    int myWins, oppWins, draws;
    string endText = "";
    Color endColor = Ui.Txt;

    ChessBoardControl chess;
    TicTacToeControl ttt;
    Label lblOpp, lblMe, lblStatus, lblScore;
    TextBox txtLog, txtMsg;
    Button btnRematch, btnLeave;

    public GameForm(NetPeer peer, GameType type, string me, string opp, bool first)
    {
        this.peer = peer;
        this.type = type;
        this.me = me;
        this.opp = opp;
        this.first = first;

        BuildUi();
        StartRound();
        Log($"Connected with {opp}. " + (first ? "You move first." : $"{opp} moves first."));

        peer.LineReceived += OnLine;
        peer.Disconnected += OnDisconnected;
        peer.StartReceiving();

        FormClosing += (_, _) => { peer.Send("BYE"); peer.Dispose(); };
    }

    // ------------------------------------------------------------ UI
    void BuildUi()
    {
        Text = $"{Discovery.Title(type.ToString())}  -  {me} vs {opp}";
        ClientSize = new Size(1000, 680);
        MinimumSize = new Size(820, 600);
        BackColor = Ui.Bg;
        ForeColor = Ui.Txt;
        Font = Ui.F(10);
        StartPosition = FormStartPosition.CenterParent;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(16) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Control board;
        if (type == GameType.Chess) { chess = new ChessBoardControl(); board = chess; }
        else { ttt = new TicTacToeControl(); board = ttt; }
        board.Dock = DockStyle.Fill;
        board.Margin = new Padding(0, 0, 12, 0);
        root.Controls.Add(board, 0, 0);

        var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Margin = new Padding(0) };
        side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));    // opponent
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));    // status
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));    // score
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));    // chat log
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));    // chat input
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));    // buttons
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));    // me

        lblOpp = PlayerLabel();
        lblMe = PlayerLabel();
        lblStatus = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Ui.Card2,
            ForeColor = Ui.Txt, Font = Ui.F(14, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8)
        };
        lblScore = new Label
        {
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Ui.Muted, Font = Ui.F(9.5f), Margin = new Padding(0)
        };
        txtLog = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Ui.Card, ForeColor = Ui.Txt, BorderStyle = BorderStyle.None, Font = Ui.F(10),
            Margin = new Padding(0, 0, 0, 6)
        };
        txtMsg = new TextBox
        {
            Dock = DockStyle.Fill, BackColor = Ui.Card2, ForeColor = Ui.Txt, BorderStyle = BorderStyle.FixedSingle,
            Font = Ui.F(11), PlaceholderText = "Type a message and press Enter", Margin = new Padding(0, 0, 0, 6)
        };
        txtMsg.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SendChat(); }
        };

        var btns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        btns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnRematch = Ui.Btn("Rematch", Ui.Accent);
        btnLeave = Ui.Btn("Leave", Ui.Danger);
        btnRematch.Margin = new Padding(0, 4, 4, 4);
        btnLeave.Margin = new Padding(4, 4, 0, 4);
        btnRematch.Click += (_, _) => RequestRematch();
        btnLeave.Click += (_, _) => Close();
        btns.Controls.Add(btnRematch, 0, 0);
        btns.Controls.Add(btnLeave, 1, 0);

        side.Controls.Add(lblOpp, 0, 0);
        side.Controls.Add(lblStatus, 0, 1);
        side.Controls.Add(lblScore, 0, 2);
        side.Controls.Add(txtLog, 0, 3);
        side.Controls.Add(txtMsg, 0, 4);
        side.Controls.Add(btns, 0, 5);
        side.Controls.Add(lblMe, 0, 6);
        root.Controls.Add(side, 1, 0);
        Controls.Add(root);

        if (chess != null) chess.MoveMade += m =>
        {
            peer.Send($"MOVE|{m.FR},{m.FC},{m.TR},{m.TC},{(m.Promo == '\0' ? '-' : m.Promo)}");
            Log($"You: {Sq(m.FR, m.FC)}-{Sq(m.TR, m.TC)}");
            AfterMove();
        };
        if (ttt != null) ttt.MoveMade += idx =>
        {
            peer.Send("MOVE|" + idx);
            AfterMove();
        };
    }

    static Label PlayerLabel() => new Label
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Ui.Card, ForeColor = Ui.Txt,
        Font = Ui.F(12, FontStyle.Bold), Padding = new Padding(14, 0, 0, 0), Margin = new Padding(0, 0, 0, 8)
    };

    static string Sq(int r, int c) => $"{(char)('a' + c)}{8 - r}";

    void Log(string s) => txtLog.AppendText(s + "\r\n");

    // ------------------------------------------------------------ game flow
    void StartRound()
    {
        gameOver = false;
        iWant = oppWant = false;
        btnRematch.Enabled = false;
        if (type == GameType.Chess) { chess.Reset(first); chess.Active = connected; }
        else { ttt.Reset(first ? 'X' : 'O'); ttt.Active = connected; }
        UpdateScore();
        UpdateStatus();
    }

    void AfterMove()
    {
        CheckEnd();
        UpdateStatus();
    }

    void CheckEnd()
    {
        if (gameOver) return;
        int outcome;   // 1 = I won, -1 = I lost, 0 = draw
        if (type == GameType.Chess)
        {
            if (chess.Game.Result == null) return;
            char o = chess.Game.Outcome;
            outcome = o == 'D' ? 0 : ((o == 'W') == first ? 1 : -1);
            Log("Game over: " + chess.Game.Result);
        }
        else
        {
            if (!ttt.Over) return;
            outcome = ttt.Winner == 'D' ? 0 : (ttt.Winner == ttt.My ? 1 : -1);
            Log("Game over.");
        }
        gameOver = true;
        if (outcome == 1) { myWins++; endText = "You win!"; endColor = Ui.Good; }
        else if (outcome == -1) { oppWins++; endText = $"{opp} wins"; endColor = Ui.Danger; }
        else { draws++; endText = "It's a draw"; endColor = Ui.Accent2; }
        btnRematch.Enabled = connected;
        UpdateScore();
    }

    void UpdateScore() => lblScore.Text = $"You {myWins}  -  {oppWins} {opp}      (draws: {draws})";

    void UpdateStatus()
    {
        bool myTurn = type == GameType.Chess ? chess.Game.WhiteToMove == first : ttt.Turn == ttt.My;
        string mark = type == GameType.Chess ? (first ? "White" : "Black") : (first ? "X" : "O");
        string oppMark = type == GameType.Chess ? (first ? "Black" : "White") : (first ? "O" : "X");
        lblMe.Text = $"{me} (you)  -  {mark}";
        lblOpp.Text = $"{opp}  -  {oppMark}";

        if (!connected)
        {
            lblStatus.Text = $"{opp} disconnected";
            lblStatus.ForeColor = Ui.Danger;
        }
        else if (gameOver)
        {
            lblStatus.Text = endText;
            lblStatus.ForeColor = endColor;
        }
        else
        {
            bool check = type == GameType.Chess && chess.Game.InCheck(chess.Game.WhiteToMove);
            lblStatus.Text = (myTurn ? "Your turn" : $"{opp}'s turn...") + (check ? "  (check!)" : "");
            lblStatus.ForeColor = myTurn ? Ui.Good : Ui.Txt;
        }
        bool live = connected && !gameOver;
        lblMe.BackColor = live && myTurn ? Ui.Accent : Ui.Card;
        lblOpp.BackColor = live && !myTurn ? Ui.Accent : Ui.Card;
    }

    void RequestRematch()
    {
        if (!gameOver || !connected || iWant) return;
        iWant = true;
        peer.Send("REMATCH");
        btnRematch.Enabled = false;
        if (oppWant) DoRematch();
        else Log("Rematch requested - waiting for " + opp + "...");
    }

    void DoRematch()
    {
        first = !first;   // swap colours / marks
        StartRound();
        Log("New game! " + (first ? "You move first." : $"{opp} moves first."));
    }

    void SendChat()
    {
        string t = txtMsg.Text.Trim().Replace("\r", " ").Replace("\n", " ");
        if (t == "" || !connected) return;
        peer.Send("CHAT|" + t);
        Log($"{me}: {t}");
        txtMsg.Clear();
    }

    // ------------------------------------------------------------ network
    void OnLine(string line)
    {
        int i = line.IndexOf('|');
        string cmd = i < 0 ? line : line.Substring(0, i);
        string arg = i < 0 ? "" : line.Substring(i + 1);

        switch (cmd)
        {
            case "MOVE": HandleRemoteMove(arg); break;
            case "CHAT": Log($"{opp}: {arg}"); break;
            case "REMATCH":
                oppWant = true;
                if (iWant && gameOver) DoRematch();
                else Log($"{opp} wants a rematch.");
                break;
            case "BYE":
                Log($"{opp} left the game.");
                LostConnection();
                break;
        }
    }

    void OnDisconnected()
    {
        if (!connected) return;
        Log("Connection lost.");
        LostConnection();
    }

    void LostConnection()
    {
        connected = false;
        if (chess != null) chess.Active = false;
        if (ttt != null) ttt.Active = false;
        btnRematch.Enabled = false;
        UpdateStatus();
    }

    void HandleRemoteMove(string arg)
    {
        if (gameOver || !connected) return;
        try
        {
            if (type == GameType.Chess)
            {
                var p = arg.Split(',');
                var m = new Move(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), p[4] == "-" ? '\0' : p[4][0]);
                if (!chess.ApplyRemote(m)) return;
                Log($"{opp}: {Sq(m.FR, m.FC)}-{Sq(m.TR, m.TC)}");
            }
            else
            {
                if (!ttt.PlaceRemote(int.Parse(arg))) return;
            }
            AfterMove();
        }
        catch { /* ignore malformed message */ }
    }
}
