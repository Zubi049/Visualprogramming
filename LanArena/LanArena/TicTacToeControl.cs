using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanArena;

/// <summary>Custom-drawn Tic Tac Toe board with hover effect and winning-line highlight.</summary>
public class TicTacToeControl : Control
{
    static readonly int[][] Lines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 }
    };

    public char[] Cells = new char[9];
    public char My = 'X', Turn = 'X', Winner = '\0';   // Winner: 'X', 'O' or 'D' (draw)
    public bool Over, Active = true;
    public event Action<int> MoveMade;

    int[] line;
    int hover = -1, cell, x0, y0;

    public TicTacToeControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Ui.Bg;
        Reset('X');
    }

    bool CanPlay => Active && !Over && Turn == My;

    public void Reset(char myMark)
    {
        Array.Fill(Cells, ' ');
        My = myMark;
        Turn = 'X';
        Over = false;
        Winner = '\0';
        line = null;
        hover = -1;
        Invalidate();
    }

    public bool Place(int idx, char mark)
    {
        if (idx < 0 || idx > 8 || Over || Cells[idx] != ' ' || Turn != mark) return false;
        Cells[idx] = mark;
        Turn = mark == 'X' ? 'O' : 'X';
        Evaluate();
        Invalidate();
        return true;
    }

    public bool PlaceRemote(int idx) => Place(idx, My == 'X' ? 'O' : 'X');

    void Evaluate()
    {
        foreach (var l in Lines)
            if (Cells[l[0]] != ' ' && Cells[l[0]] == Cells[l[1]] && Cells[l[1]] == Cells[l[2]])
            {
                Winner = Cells[l[0]];
                line = l;
                Over = true;
                return;
            }
        if (Cells.All(c => c != ' ')) { Winner = 'D'; Over = true; }
    }

    void Geo()
    {
        int size = Math.Min(Width, Height) - 40;
        cell = Math.Max(size / 3, 1);
        x0 = (Width - cell * 3) / 2;
        y0 = (Height - cell * 3) / 2;
    }

    Rectangle CellRect(int i) => new Rectangle(x0 + (i % 3) * cell, y0 + (i / 3) * cell, cell, cell);

    int HitCell(Point p)
    {
        Geo();
        int c = (p.X - x0) / cell, r = (p.Y - y0) / cell;
        if (p.X < x0 || p.Y < y0 || c < 0 || c > 2 || r < 0 || r > 2) return -1;
        return r * 3 + c;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Bg);
        Geo();
        if (cell < 30) return;

        for (int i = 0; i < 9; i++)
        {
            var rr = Rectangle.Inflate(CellRect(i), -6, -6);
            Color bg = Ui.Card;
            if (hover == i && CanPlay && Cells[i] == ' ') bg = Ui.Card2;
            if (line != null && line.Contains(i)) bg = Color.FromArgb(70, 85, 239, 196);
            using (var b = new SolidBrush(bg)) Ui.FillRound(g, b, rr, 16);

            float pad = rr.Width * 0.24f;
            float pw = Math.Max(4f, cell * 0.085f);
            if (Cells[i] == 'X')
            {
                using var pen = new Pen(Ui.Danger, pw) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawLine(pen, rr.Left + pad, rr.Top + pad, rr.Right - pad, rr.Bottom - pad);
                g.DrawLine(pen, rr.Right - pad, rr.Top + pad, rr.Left + pad, rr.Bottom - pad);
            }
            else if (Cells[i] == 'O')
            {
                using var pen = new Pen(Ui.Accent2, pw);
                g.DrawEllipse(pen, rr.Left + pad, rr.Top + pad, rr.Width - 2 * pad, rr.Height - 2 * pad);
            }
        }

        if (line != null)
        {
            var a = CellRect(line[0]);
            var b = CellRect(line[2]);
            using var pen = new Pen(Color.FromArgb(230, 255, 255, 255), Math.Max(4f, cell * 0.05f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, a.Left + cell / 2, a.Top + cell / 2, b.Left + cell / 2, b.Top + cell / 2);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = HitCell(e.Location);
        if (h != hover) { hover = h; Invalidate(); }
        Cursor = (CanPlay && h >= 0 && Cells[h] == ' ') ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hover = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!CanPlay) return;
        int i = HitCell(e.Location);
        if (i >= 0 && Place(i, My)) MoveMade?.Invoke(i);
    }
}
