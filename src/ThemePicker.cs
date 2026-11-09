using System;
using System.Drawing;
using System.Windows.Forms;

// The theme picker, opened from "Choose theme..." in the main window. Clicking a theme applies it
// at once (here, in the main window, the banner and the tray icon); Done or Esc closes it.
class ThemePicker : Form
{
    const int Width96 = 540, Pad = 24, Inner = Width96 - 2 * Pad;
    readonly Settings settings;
    readonly Action picked;
    readonly Label title = new Label(), note = new Label();
    readonly PillButton done = new PillButton();

    public ThemePicker(Settings settings, Action picked)
    {
        this.settings = settings;
        this.picked = picked;

        Text = "Choose a theme";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        Font = Ui.Font(10f);
        ClientSize = new Size(Ui.S(Width96), Ui.S(268));

        title.Text = "Choose a theme";
        title.Font = Ui.Heading(15f);
        title.AutoSize = true;
        title.Location = new Point(Ui.S(Pad), Ui.S(18));
        Controls.Add(title);
        note.Text = "Colours the window, the reminder banner and the tray icon.";
        note.Font = Ui.Font(9.5f);
        note.AutoSize = true;
        note.Location = new Point(Ui.S(Pad + 1), Ui.S(54));
        Controls.Add(note);

        const int columns = 3, gap = 8, cardHeight = 56;
        int cardWidth = (Inner - gap * (columns - 1)) / columns;
        for (int i = 0; i < Theme.All.Length; i++)
        {
            // 9 pt so the longest name, "Monochrome", fits the card
            var card = new ThemeCard(Theme.All[i]) { Font = Ui.Font(9f) };
            card.Bounds = new Rectangle(Ui.S(Pad + (i % columns) * (cardWidth + gap)), Ui.S(90 + (i / columns) * (cardHeight + gap)),
                Ui.S(cardWidth), Ui.S(cardHeight));
            card.Click += delegate
            {
                settings.Theme = card.Own.Name;
                ApplyTheme();
                picked();
            };
            Controls.Add(card);
        }

        done.Text = "Done";
        done.Primary = true;
        done.Bounds = new Rectangle(Ui.S(Width96 - Pad - 110), Ui.S(220), Ui.S(110), Ui.S(32));
        done.Click += delegate { Close(); };
        Controls.Add(done);

        ApplyTheme();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    void ApplyTheme()
    {
        var t = Theme.Find(settings.Theme);
        BackColor = t.Bg;
        foreach (Control control in Controls) Ui.Recolor(control, t, t.Bg);
        title.ForeColor = t.Text;
        note.ForeColor = t.Text3;
        Ui.StyleTitleBar(this, t);
    }
}
