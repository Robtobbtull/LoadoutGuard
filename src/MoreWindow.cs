using System.Drawing;
using System.Windows.Forms;

// "More..." in the main window: the version, Check for updates and Uninstall.
// It only records the choice and closes; the main window acts on it, since both may exit LoadoutGuard.
class MoreWindow : Form
{
    const int Width96 = 420, Pad = 24, Inner = Width96 - 2 * Pad, Gap = 12, ButtonW = (Inner - Gap) / 2;
    readonly Label title = new Label(), version = new Label(), note = new Label();

    public bool UpdateChosen, UninstallChosen;

    public MoreWindow(Settings settings)
    {
        Text = "More";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        Font = Ui.Font(10f);
        ClientSize = new Size(Ui.S(Width96), Ui.S(236));

        title.Text = "LoadoutGuard";
        title.Font = Ui.Heading(15f);
        PlaceText(title, Pad, 18);
        version.Text = "Version " + Updater.Current;
        version.Font = Ui.Font(9.5f);
        PlaceText(version, Pad + 1, 54);

        // two equal buttons side by side, with room around them
        AddButton("Check for updates", Pad, delegate { UpdateChosen = true; });
        AddButton("Uninstall...", Pad + ButtonW + Gap, delegate { UninstallChosen = true; });

        note.Text = "Checking for updates is the only time LoadoutGuard goes online.";
        note.Font = Ui.Font(9f);
        note.MaximumSize = new Size(Ui.S(Inner), 0); // wraps rather than running off the edge
        PlaceText(note, Pad + 1, 142);

        var done = new PillButton { Text = "Done", Primary = true };
        done.Bounds = new Rectangle(Ui.S(Width96 - Pad - 110), Ui.S(184), Ui.S(110), Ui.S(36));
        done.Click += delegate { Close(); };
        Controls.Add(done);

        var t = Theme.Find(settings.Theme);
        BackColor = t.Bg;
        foreach (Control control in Controls) Ui.Recolor(control, t, t.Bg);
        title.ForeColor = t.Text;
        version.ForeColor = note.ForeColor = t.Text3;
        Ui.StyleTitleBar(this, t);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    void AddButton(string text, int x, System.Action choose)
    {
        var pill = new PillButton { Text = text };
        pill.Bounds = new Rectangle(Ui.S(x), Ui.S(92), Ui.S(ButtonW), Ui.S(36));
        pill.Click += delegate { choose(); Close(); };
        Controls.Add(pill);
    }

    void PlaceText(Label label, int x, int y)
    {
        label.AutoSize = true;
        label.Location = new Point(Ui.S(x), Ui.S(y));
        Controls.Add(label);
    }
}
