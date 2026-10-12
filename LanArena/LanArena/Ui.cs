using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LanArena;

/// <summary>Shared colours + small helpers so every screen looks consistent.</summary>
public static class Ui
{
    public static readonly Color Bg      = Color.FromArgb(14, 17, 31);
    public static readonly Color Card    = Color.FromArgb(24, 28, 48);
    public static readonly Color Card2   = Color.FromArgb(36, 41, 70);
    public static readonly Color Accent  = Color.FromArgb(108, 92, 231);
    public static readonly Color Accent2 = Color.FromArgb(0, 206, 201);
    public static readonly Color Txt     = Color.FromArgb(236, 239, 248);
    public static readonly Color Muted   = Color.FromArgb(138, 145, 173);
    public static readonly Color Danger  = Color.FromArgb(255, 107, 129);
    public static readonly Color Good    = Color.FromArgb(85, 239, 196);

    public static Font F(float size, FontStyle style = FontStyle.Regular) => new Font("Segoe UI", size, style);

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void FillRound(Graphics g, Brush b, Rectangle r, int radius)
    {
        using var p = Round(r, radius);
        g.FillPath(b, p);
    }

    public static Button Btn(string text, Color color)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = F(10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.25f);
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.1f);
        return b;
    }

    public static Label Lbl(string text, float size, Color color, bool bold = false) => new Label
    {
        Text = text,
        AutoSize = true,
        ForeColor = color,
        BackColor = Color.Transparent,
        Font = F(size, bold ? FontStyle.Bold : FontStyle.Regular)
    };

    public static TextBox Input() => new TextBox
    {
        BackColor = Card2,
        ForeColor = Txt,
        BorderStyle = BorderStyle.FixedSingle,
        Font = F(12),
        Dock = DockStyle.Fill,
        Margin = new Padding(4)
    };
}

public class GradientPanel : Panel
{
    public GradientPanel() { DoubleBuffered = true; }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width > 0 && Height > 0)
        {
            using var b = new LinearGradientBrush(ClientRectangle, Ui.Accent, Ui.Accent2, 0f);
            e.Graphics.FillRectangle(b, ClientRectangle);
        }
        base.OnPaint(e);
    }
}
