using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Drawing helpers and the few custom controls the window needs. Everything is painted with the
// current Theme. Sizes are written for 100% Windows scaling and multiplied by Ui.Scale.
static class Ui
{
    public static float Scale = 1f; // set once at start: screen DPI times Fit
    static float fit = 1f;

    public static readonly string TextFamily = Pick("Segoe UI Variable Text", "Segoe UI");
    public static readonly string HeadingFamily = Pick("Segoe UI Variable Display Semibold", "Segoe UI Semibold");

    // Call once at start, before any window is made. Shrinks sizes and fonts alike when the screen
    // is too short for content this tall (in pixels at 100% scaling), e.g. 1366x768, or 1080p at 150%.
    public static void FitHeight(int height96, int available)
    {
        fit = Math.Max(0.6f, Math.Min(1f, available / (height96 * Scale)));
        Scale *= fit;
    }

    public static int S(float value)
    {
        return (int)Math.Round(value * Scale);
    }

    public static Font Font(float points)
    {
        return new Font(TextFamily, points * fit);
    }

    public static Font Heading(float points)
    {
        return new Font(HeadingFamily, points * fit);
    }

    // Windows 11 has Segoe UI Variable; Windows 10 falls back to Segoe UI.
    static string Pick(string preferred, string fallback)
    {
        using (var font = new Font(preferred, 10f))
            return font.Name == preferred ? preferred : fallback;
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // A rounded box filling bounds, with an optional border drawn just inside its edge.
    public static void Box(Graphics g, Rectangle bounds, float radius, Color fill, Color border, float borderWidth)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float inset = borderWidth / 2f;
        var r = new RectangleF(bounds.X + inset, bounds.Y + inset, bounds.Width - borderWidth, bounds.Height - borderWidth);
        using (var path = Round(r, radius))
        {
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            if (borderWidth > 0)
                using (var pen = new Pen(border, borderWidth)) g.DrawPath(pen, path);
        }
    }

    public static void Text(Graphics g, string text, Font font, Rectangle bounds, Color color, Color back, TextFormatFlags flags)
    {
        TextRenderer.DrawText(g, text, font, bounds, color, back, flags | TextFormatFlags.NoPadding);
    }

    // The LoadoutGuard mark: a shield with a check.
    public static void Shield(Graphics g, RectangleF r, Color fill, Color check)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float x = r.X, y = r.Y, w = r.Width, h = r.Height;
        using (var path = new GraphicsPath())
        {
            path.AddLine(x + w * 0.5f, y, x + w * 0.92f, y + h * 0.17f);
            path.AddLine(x + w * 0.92f, y + h * 0.17f, x + w * 0.92f, y + h * 0.47f);
            path.AddBezier(x + w * 0.92f, y + h * 0.47f, x + w * 0.92f, y + h * 0.74f, x + w * 0.73f, y + h * 0.9f, x + w * 0.5f, y + h);
            path.AddBezier(x + w * 0.5f, y + h, x + w * 0.27f, y + h * 0.9f, x + w * 0.08f, y + h * 0.74f, x + w * 0.08f, y + h * 0.47f);
            path.AddLine(x + w * 0.08f, y + h * 0.47f, x + w * 0.08f, y + h * 0.17f);
            path.CloseFigure();
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
        }
        using (var pen = new Pen(check, Math.Max(1.5f, w * 0.1f)))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, new[]
            {
                new PointF(x + w * 0.31f, y + h * 0.5f),
                new PointF(x + w * 0.45f, y + h * 0.64f),
                new PointF(x + w * 0.7f, y + h * 0.37f),
            });
        }
    }

    // Window and tray icon in the theme's accent colour.
    public static Icon MakeIcon(Theme theme)
    {
        using (var bitmap = new Bitmap(32, 32))
        {
            using (var g = Graphics.FromImage(bitmap)) Shield(g, new RectangleF(2, 1, 28, 30), theme.Accent, theme.AccentInk);
            // shortcut: the icon handle is never freed; it's made once per theme change, a few hundred bytes each
            return Icon.FromHandle(bitmap.GetHicon());
        }
    }

    // Gives a control (and everything inside a card) the theme's colours.
    public static void Recolor(Control control, Theme theme, Color under)
    {
        control.BackColor = under;
        var card = control as Card;
        if (card != null)
        {
            card.Theme = theme;
            foreach (Control child in card.Controls) Recolor(child, theme, theme.Surface);
            return;
        }
        var painted = control as PaintedControl;
        if (painted != null) painted.Theme = theme;
    }

    // Title bar in the theme's colours on Windows 11; dark mode only on Windows 10.
    public static void StyleTitleBar(Form form, Theme theme)
    {
        SetDwm(form, 20, theme.Dark ? 1 : 0);
        SetDwm(form, 35, ColorTranslator.ToWin32(theme.Bg));
        SetDwm(form, 36, ColorTranslator.ToWin32(theme.Text));
        SetDwm(form, 34, ColorTranslator.ToWin32(theme.Line));
    }

    // The monitor of the active window: the game's, when a reminder comes from playing.
    public static Screen ActiveScreen()
    {
        return Screen.FromHandle(GetForegroundWindow());
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    static void SetDwm(Form form, int attribute, int value)
    {
        DwmSetWindowAttribute(form.Handle, attribute, ref value, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

// Base for the custom controls: double-buffered, painted entirely by the subclass.
class PaintedControl : Control
{
    protected Theme theme = Theme.All[0];

    public PaintedControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public Theme Theme
    {
        get { return theme; }
        set { theme = value; Invalidate(); }
    }

    // keyboard focus shows a ring; mouse clicks don't
    protected bool FocusRing
    {
        get { return Focused && ShowFocusCues; }
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}

// A rounded panel with optional divider lines between rows.
class Card : Panel
{
    public readonly List<int> Dividers = new List<int>(); // y positions, already scaled
    Theme theme = Theme.All[0];

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    public Theme Theme
    {
        get { return theme; }
        set { theme = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Ui.Box(g, ClientRectangle, Ui.S(12), theme.Surface, theme.Line, 1);
        using (var pen = new Pen(theme.Line))
            foreach (int y in Dividers) g.DrawLine(pen, Ui.S(16), y, Width - Ui.S(16), y);
    }
}

// An on/off switch.
class Toggle : PaintedControl
{
    bool on;
    public event EventHandler CheckedChanged;

    public Toggle()
    {
        Size = new Size(Ui.S(38), Ui.S(22));
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public bool Checked
    {
        get { return on; }
        set
        {
            if (on == value) return;
            on = value;
            Invalidate();
            var handler = CheckedChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        Focus();
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) Checked = !Checked;
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var r = ClientRectangle;
        Color border = FocusRing ? theme.Accent2 : on ? theme.Accent : theme.Line2;
        Ui.Box(g, r, r.Height / 2f, on ? theme.Accent : theme.Surface2, border, FocusRing ? Ui.S(2) : 1);
        int pad = Ui.S(3), d = r.Height - 2 * pad;
        using (var knob = new SolidBrush(on ? theme.AccentInk : theme.Text2))
            g.FillEllipse(knob, on ? r.Width - pad - d : pad, pad, d, d);
    }
}

// A rounded button; Primary ones are filled with the accent colour.
class PillButton : PaintedControl
{
    public bool Primary;
    bool hover, down;

    public PillButton()
    {
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Color fill = Primary ? (hover ? theme.Accent2 : theme.Accent) : (hover ? theme.Surface2 : theme.Surface);
        if (down) fill = Theme.Mix(fill, theme.Bg, 0.15);
        Color border = FocusRing ? theme.Accent : Primary ? fill : theme.Line2;
        Ui.Box(g, ClientRectangle, Ui.S(8), fill, border, FocusRing ? Ui.S(2) : 1);
        Ui.Text(g, Text, Font, ClientRectangle, Primary ? theme.AccentInk : theme.Text, fill,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

// A theme to pick: a small preview of its colours with its name below.
class ThemeCard : PaintedControl
{
    public readonly Theme Own;
    bool hover;

    public ThemeCard(Theme own)
    {
        Own = own;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    bool Chosen
    {
        get { return theme == Own; }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        var r = ClientRectangle;
        Color border = Chosen || FocusRing ? theme.Accent : hover ? theme.Text3 : theme.Line2;
        Ui.Box(g, r, Ui.S(12), theme.Side, border, Chosen ? Ui.S(2) : 1);

        // swatch on the left: the theme's background with a text bar and accent + text dots
        int pad = Ui.S(7);
        var swatch = new Rectangle(pad, pad, Ui.S(34), r.Height - 2 * pad);
        Ui.Box(g, swatch, Ui.S(7), Own.Bg, Own.Line2, 1);
        var bar = new Rectangle(swatch.X + Ui.S(6), swatch.Y + Ui.S(8), Ui.S(20), Ui.S(5));
        Ui.Box(g, bar, Ui.S(2.5f), Own.Text3, Own.Text3, 0);
        int dot = Ui.S(9), y = swatch.Bottom - Ui.S(7) - dot;
        using (var accent = new SolidBrush(Own.Accent)) g.FillEllipse(accent, swatch.X + Ui.S(6), y, dot, dot);
        using (var text = new SolidBrush(Own.Text)) g.FillEllipse(text, swatch.X + Ui.S(6) + dot + Ui.S(3), y, dot, dot);

        var label = new Rectangle(swatch.Right + Ui.S(8), 0, r.Width - swatch.Right - Ui.S(14), r.Height);
        Ui.Text(g, Own.Name, Font, label, Chosen ? theme.Text : theme.Text2, theme.Side,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

// A read-only path in a rounded box, shortened in the middle when too long.
class PathBox : PaintedControl
{
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        Ui.Box(g, ClientRectangle, Ui.S(8), theme.Side, theme.Line, 1);
        var inner = Rectangle.Inflate(ClientRectangle, -Ui.S(12), 0);
        Ui.Text(g, Text, Font, inner, theme.Text2, theme.Side,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis);
    }
}

// The shield mark next to the title.
class Mark : PaintedControl
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        Ui.Shield(e.Graphics, ClientRectangle, theme.Accent, theme.Dark ? theme.Bg : theme.AccentInk);
    }
}
