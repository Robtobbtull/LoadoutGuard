using System;
using System.Drawing;
using System.Windows.Forms;

// A small message window in the app's theme, used instead of plain Windows message boxes.
// Not modal, so the tray app keeps running; Set() changes its text and buttons in place,
// so one popup can go from "Checking..." to the result.
class Popup : Form
{
    const int Width96 = 420, Pad = 24, Inner = Width96 - 2 * Pad;
    readonly Label heading = new Label(), message = new Label();
    readonly PillButton primary = new PillButton { Primary = true }, secondary = new PillButton();
    Action onPrimary;

    public Popup(Theme theme)
    {
        Text = "LoadoutGuard";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        Icon = Ui.MakeIcon(theme);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Font = Ui.Font(10f);
        ClientSize = new Size(Ui.S(Width96), Ui.S(150));

        heading.Font = Ui.Heading(14f);
        heading.AutoSize = true;
        heading.Location = new Point(Ui.S(Pad), Ui.S(20));
        message.Font = Ui.Font(10f);
        message.AutoSize = true;
        message.MaximumSize = new Size(Ui.S(Inner), 0); // wraps long text
        message.Location = new Point(Ui.S(Pad), Ui.S(58));
        primary.Click += delegate { if (onPrimary != null) onPrimary(); };
        secondary.Click += delegate { Close(); };
        Controls.AddRange(new Control[] { heading, message, primary, secondary });

        BackColor = theme.Bg;
        foreach (Control control in Controls) Ui.Recolor(control, theme, theme.Bg);
        heading.ForeColor = theme.Text;
        message.ForeColor = theme.Text2;
        Ui.StyleTitleBar(this, theme);
    }

    // primaryText null = no main button; closeText null = no close button (the title bar X still closes it).
    public void Set(string headingText, string messageText, string primaryText, Action onPrimary, string closeText)
    {
        heading.Text = headingText;
        message.Text = messageText;
        this.onPrimary = onPrimary;

        // Visible reads false until the form shows, so these say what is meant to show
        bool hasPrimary = primaryText != null, hasClose = closeText != null;
        int top = message.Bottom + Ui.S(22), right = Ui.S(Pad + Inner);
        primary.Visible = hasPrimary;
        if (hasPrimary)
        {
            primary.Text = primaryText;
            primary.Bounds = new Rectangle(right - Ui.S(130), top, Ui.S(130), Ui.S(32));
            right -= Ui.S(130 + 10);
        }
        secondary.Visible = hasClose;
        if (hasClose)
        {
            secondary.Text = closeText;
            secondary.Bounds = new Rectangle(right - Ui.S(100), top, Ui.S(100), Ui.S(32));
        }
        ClientSize = new Size(ClientSize.Width, hasPrimary || hasClose ? top + Ui.S(32 + 20) : message.Bottom + Ui.S(24));

        if (!Visible) Show();
        Activate();
        if (hasPrimary) primary.Focus();
        else if (hasClose) secondary.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }
}
