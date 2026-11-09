using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// The reminder banner at the top of the screen: a rounded card with the shield mark, a title and
// a line of detail. Stays above the game without taking focus, so the game keeps mouse and keyboard.
class ReminderForm : Form
{
    const string Hint = "Click to close";
    readonly Timer hideTimer = new Timer();
    readonly Font titleFont = Ui.Heading(13f), bodyFont = Ui.Font(10.5f), hintFont = Ui.Font(9f);
    Theme theme = Theme.All[0];
    string title = "", body = "";

    public ReminderForm()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Opacity = 0.97;
        Cursor = Cursors.Hand;
        Click += delegate { Dismiss(); };
        hideTimer.Tick += delegate { Dismiss(); };
    }

    void Dismiss()
    {
        hideTimer.Stop();
        Hide();
    }

    public void ApplyTheme(Theme value)
    {
        theme = value;
        Invalidate();
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2; // DWMWCP_ROUND: rounded window corners on Windows 11, ignored on Windows 10
        DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
    }

    public void ShowMessage(string message, int seconds)
    {
        int newline = message.IndexOf('\n');
        title = newline < 0 ? message : message.Substring(0, newline);
        body = newline < 0 ? "" : message.Substring(newline + 1);

        var screen = Ui.ActiveScreen().Bounds;
        Width = Math.Min(Ui.S(580), screen.Width - Ui.S(32)); // narrower on small or portrait screens
        var text = TextArea();
        int height = Ui.S(18) + Measure(title, titleFont, text.Width) + Ui.S(4);
        if (body.Length > 0) height += Measure(body, bodyFont, text.Width) + Ui.S(6);
        height += Measure(Hint, hintFont, text.Width) + Ui.S(18);
        Height = height;

        Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top + Ui.S(48));
        hideTimer.Stop();
        hideTimer.Interval = Math.Max(1, seconds) * 1000;
        Invalidate();
        if (!Visible) Show();
        hideTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(theme.Surface);
        Ui.Box(g, ClientRectangle, Ui.S(8), theme.Surface, theme.Accent, Ui.S(1.5f));
        Ui.Shield(g, new RectangleF(Ui.S(20), Ui.S(18), Ui.S(26), Ui.S(29)), theme.Accent, theme.Dark ? theme.Surface : theme.AccentInk);

        var text = TextArea();
        var flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak;
        int y = Ui.S(18);
        y += Draw(g, title, titleFont, text, y, theme.Text, flags) + Ui.S(4);
        if (body.Length > 0) y += Draw(g, body, bodyFont, text, y, theme.Text2, flags) + Ui.S(6);
        Draw(g, Hint, hintFont, text, y, theme.Text3, flags);
    }

    int Draw(Graphics g, string s, Font font, Rectangle area, int y, Color color, TextFormatFlags flags)
    {
        int height = Measure(s, font, area.Width);
        Ui.Text(g, s, font, new Rectangle(area.X, y, area.Width, height), color, theme.Surface, flags);
        return height;
    }

    Rectangle TextArea()
    {
        int left = Ui.S(20 + 26 + 16);
        return new Rectangle(left, 0, Width - left - Ui.S(22), 0);
    }

    static int Measure(string s, Font font, int width)
    {
        return TextRenderer.MeasureText(s, font, new Size(width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
