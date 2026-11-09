using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// The LoadoutGuard window: status, the banner, the red flash, theme and Game.log.
// Every change is saved and applied at once; closing the window keeps LoadoutGuard in the tray.
class MainWindow : Form
{
    static readonly int[] SecondSteps = { 5, 10, 15, 20, 30, 45, 60, 90, 120 };
    static readonly int[] FlashSteps = { 3, 5, 10, 20, 30, 60, 0 }; // 0 = until the banner closes
    public const int Height96 = 626;
    const int Width96 = 540, Pad = 24, Inner = Width96 - 2 * Pad, Right = Inner - 18, RowH = 48;
    const string FooterText = "Closing this window keeps LoadoutGuard running in the tray.";

    readonly Settings settings;
    readonly Action changed, testBanner;
    readonly Mark mark = new Mark();
    readonly Label title = new Label(), logStatus = new Label(), editorStatus = new Label(), footer = new Label();
    readonly Label secondsValue = new Label(), flashValue = new Label(), themeName = new Label();
    readonly Dot logDot = new Dot(), editorDot = new Dot();
    readonly Toggle startup = new Toggle(), flash = new Toggle();
    readonly PillButton fewer = new PillButton(), more = new PillButton(), test = new PillButton();
    readonly PillButton shorter = new PillButton(), longer = new PillButton();
    readonly PillButton chooseTheme = new PillButton(), browse = new PillButton();
    readonly PathBox logPath = new PathBox();
    readonly TextBox customMessage = new TextBox();
    readonly List<Label> sections = new List<Label>(), rowTitles = new List<Label>(), rowNotes = new List<Label>();
    bool logFound, editorOpen, watchingSave;

    public MainWindow(Settings settings, Action changed, Action testBanner)
    {
        this.settings = settings;
        this.changed = changed;
        this.testBanner = testBanner;

        Text = "LoadoutGuard";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        Font = Ui.Font(10f);
        ClientSize = new Size(Ui.S(Width96), Ui.S(Height96)); // Ui shrinks it on screens too small for it

        // header
        Place(this, mark, Pad, 24, 28, 30);
        title.Text = "LoadoutGuard";
        title.Font = Ui.Heading(19f);
        PlaceText(this, title, 62, 17);
        Place(this, logDot, Pad + 2, 73, 8, 8);
        PlaceText(this, logStatus, Pad + 16, 65);
        Place(this, editorDot, Pad + 2, 95, 8, 8);
        PlaceText(this, editorStatus, Pad + 16, 87);

        // banner
        Section("Banner", 122);
        var banner = AddCard(142, 2, RowH);

        RowTitle(banner, 0, "Show for");
        fewer.Text = "−";
        more.Text = "+";
        Stepper(banner, 0, fewer, secondsValue, more, 64);
        ShowSeconds();
        fewer.Click += delegate { SetSeconds(Step(settings.BannerSeconds, -1)); };
        more.Click += delegate { SetSeconds(Step(settings.BannerSeconds, +1)); };

        RowTitle(banner, 1, "Message");
        // the player's own text for the Save reminder; empty shows the standard message.
        // A borderless box inside an empty PathBox, so it gets the same rounded frame as the Game.log path.
        var messageFrame = new PathBox { Cursor = Cursors.IBeam };
        Place(banner, messageFrame, 100, RowH + 8, Right - 124 - 10 - 100, 32);
        messageFrame.Click += delegate { customMessage.Focus(); };
        customMessage.BorderStyle = BorderStyle.None;
        customMessage.MaxLength = 150;
        customMessage.Text = settings.CustomMessage; // before the handler, so loading doesn't count as a change
        Place(banner, customMessage, 100 + 10, RowH, Right - 124 - 10 - 100 - 20, 20);
        customMessage.Top = messageFrame.Top + (messageFrame.Height - customMessage.Height) / 2;
        customMessage.BringToFront();
        SendMessage(customMessage.Handle, EM_SETCUEBANNER, (IntPtr)1, "Your own Save reminder text");
        customMessage.TextChanged += delegate { settings.CustomMessage = customMessage.Text.Trim(); SaveSettings(); };
        customMessage.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) e.SuppressKeyPress = true; }; // no error ding
        test.Text = "Test banner";
        test.Primary = true;
        Place(banner, test, Right - 124, RowH + 8, 124, 32);
        test.Click += delegate { testBanner(); };

        // red flash
        Section("Red flash", 256);
        var red = AddCard(276, 2, RowH);
        ToggleRow(red, 0, flash, "Flash the screen red", "A light red blink over the game; it stays visible",
            settings.Flash, delegate { settings.Flash = flash.Checked; });
        var flashLabel = new Label { Text = "Flash for", Font = Ui.Font(10.5f) };
        PlaceText(red, flashLabel, 18, RowH + 5);
        rowTitles.Add(flashLabel);
        var flashNote = new Label { Text = "Seconds, or until the banner closes", Font = Ui.Font(9f) };
        PlaceText(red, flashNote, 18, RowH + 26);
        rowNotes.Add(flashNote);
        shorter.Text = "−";
        longer.Text = "+";
        Stepper(red, 1, shorter, flashValue, longer, 104); // wide enough for "Until closed"
        ShowFlashSeconds();
        shorter.Click += delegate { SetFlashSeconds(-1); };
        longer.Click += delegate { SetFlashSeconds(+1); };

        // theme and startup
        Section("General", 390);
        var general = AddCard(410, 2, RowH);
        var themeLabel = new Label { Text = "Theme", Font = Ui.Font(10.5f) };
        PlaceText(general, themeLabel, 18, 5);
        rowTitles.Add(themeLabel);
        themeName.Font = Ui.Font(9f);
        PlaceText(general, themeName, 18, 26);
        rowNotes.Add(themeName);
        chooseTheme.Text = "Choose theme...";
        Place(general, chooseTheme, Right - 150, 8, 150, 32);
        chooseTheme.Click += delegate
        {
            using (var picker = new ThemePicker(settings, delegate { ApplyTheme(); Changed(); }))
                picker.ShowDialog(this);
        };
        ToggleRow(general, 1, startup, "Start with Windows", "Opens quietly in the tray when you sign in",
            AutoStart.Enabled, delegate { SetAutoStart(); });

        // Game.log
        Section("Path to Game.log", 524);
        logPath.Text = settings.GameLog;
        Place(this, logPath, Pad, 544, Inner - 108, 36);
        browse.Text = "Browse...";
        Place(this, browse, Pad + Inner - 100, 544, 100, 36);
        browse.Click += delegate { Browse(); };

        footer.Text = FooterText;
        footer.Font = Ui.Font(9f);
        PlaceText(this, footer, Pad, 594);

        ApplyTheme();
        Shown += delegate { ActiveControl = null; }; // no blinking caret in the message box on open
    }

    // Called by TrayApp every second while the window is open.
    public void ShowStatus(bool logFound, bool editorOpen, bool watchingSave)
    {
        this.logFound = logFound;
        this.editorOpen = editorOpen;
        this.watchingSave = watchingSave;
        var t = Theme.Find(settings.Theme);
        logStatus.Text = logFound ? "Watching Game.log" : "Game.log not found. Set where it is below.";
        logDot.Color = logFound ? t.Accent : t.Danger;
        editorStatus.Text = !editorOpen ? "Loadout editor not open"
            : watchingSave ? "Loadout editor open, watching for Save clicks"
            : "Loadout editor open";
        editorDot.Color = editorOpen ? t.Accent : t.Text3;
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
        Icon = Ui.MakeIcon(t);
        Ui.StyleTitleBar(this, t);

        title.ForeColor = t.Text;
        logStatus.ForeColor = editorStatus.ForeColor = t.Text2;
        secondsValue.ForeColor = flashValue.ForeColor = customMessage.ForeColor = t.Text;
        customMessage.BackColor = t.Side;
        footer.ForeColor = t.Text3;
        foreach (var label in sections) label.ForeColor = t.Text3;
        foreach (var label in rowTitles) label.ForeColor = t.Text;
        foreach (var label in rowNotes) label.ForeColor = t.Text3;
        themeName.Text = t.Name;

        ShowStatus(logFound, editorOpen, watchingSave);
    }

    void SetAutoStart()
    {
        if (startup.Checked == AutoStart.Enabled) return; // already so (this also ends the undo below)
        try
        {
            AutoStart.Enabled = startup.Checked;
        }
        catch (Exception e)
        {
            if (!AutoStart.IsRegistryError(e)) throw;
            startup.Checked = AutoStart.Enabled; // show what is really set
            MessageBox.Show(this, "Couldn't change Start with Windows:\n" + e.Message, "LoadoutGuard");
        }
    }

    void SetSeconds(int value)
    {
        settings.BannerSeconds = value;
        ShowSeconds();
        Changed();
    }

    void ShowSeconds()
    {
        secondsValue.Text = settings.BannerSeconds + " s";
    }

    static int Step(int current, int direction)
    {
        if (direction > 0)
        {
            foreach (int s in SecondSteps) if (s > current) return s;
            return SecondSteps[SecondSteps.Length - 1];
        }
        for (int i = SecondSteps.Length - 1; i >= 0; i--) if (SecondSteps[i] < current) return SecondSteps[i];
        return SecondSteps[0];
    }

    // One step along FlashSteps; a value typed into settings.ini that isn't a step starts from the first.
    void SetFlashSeconds(int direction)
    {
        int i = Math.Max(0, Array.IndexOf(FlashSteps, settings.FlashSeconds)) + direction;
        settings.FlashSeconds = FlashSteps[Math.Max(0, Math.Min(FlashSteps.Length - 1, i))];
        ShowFlashSeconds();
        Changed();
    }

    void ShowFlashSeconds()
    {
        flashValue.Text = settings.FlashSeconds == 0 ? "Until closed" : settings.FlashSeconds + " s";
    }

    void Browse()
    {
        using (var dialog = new OpenFileDialog())
        {
            dialog.Title = "Find Star Citizen's Game.log (in the LIVE or PTU folder)";
            dialog.Filter = "Game.log|Game.log|Log files (*.log)|*.log";
            var folder = Path.GetDirectoryName(settings.GameLog);
            if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            settings.GameLog = dialog.FileName;
            logPath.Text = dialog.FileName;
            Changed();
        }
    }

    void Changed()
    {
        SaveSettings();
        changed();
    }

    // Writes settings.ini only; for changes the tray app needs nothing to apply (it reads the shared settings).
    void SaveSettings()
    {
        try
        {
            settings.Save(Settings.FilePath);
            footer.Text = FooterText;
        }
        catch (Exception e)
        {
            if (!(e is IOException || e is UnauthorizedAccessException)) throw;
            footer.Text = "Could not save settings: " + e.Message;
        }
    }

    // ---- layout helpers (positions at 100% scaling) ----

    void Section(string text, int y)
    {
        var label = new Label { Text = text, Font = Ui.Font(9f) };
        PlaceText(this, label, Pad + 2, y);
        sections.Add(label);
    }

    Card AddCard(int y, int rows, int rowHeight)
    {
        var card = new Card();
        Place(this, card, Pad, y, Inner, rows * rowHeight);
        for (int i = 1; i < rows; i++) card.Dividers.Add(Ui.S(i * rowHeight));
        return card;
    }

    // A one-line row title, centred vertically in its row.
    void RowTitle(Card card, int row, string text)
    {
        var label = new Label { Text = text, Font = Ui.Font(10.5f) };
        PlaceText(card, label, 18, row * RowH);
        label.Top = Ui.S(row * RowH) + (Ui.S(RowH) - label.Height) / 2;
        rowTitles.Add(label);
    }

    // [−] value [+] at the right of a row.
    static void Stepper(Card card, int row, PillButton minus, Label value, PillButton plus, int valueWidth)
    {
        int top = row * RowH + 8;
        Place(card, plus, Right - 32, top, 32, 32);
        value.TextAlign = ContentAlignment.MiddleCenter;
        Place(card, value, Right - 32 - valueWidth, top, valueWidth, 32);
        Place(card, minus, Right - 32 - valueWidth - 32, top, 32, 32);
    }

    void ToggleRow(Card card, int row, Toggle toggle, string text, string note, bool value, Action set)
    {
        int top = row * RowH;
        var label = new Label { Text = text, Font = Ui.Font(10.5f), Cursor = Cursors.Hand };
        PlaceText(card, label, 18, top + 5);
        var small = new Label { Text = note, Font = Ui.Font(9f), Cursor = Cursors.Hand };
        PlaceText(card, small, 18, top + 26);
        rowTitles.Add(label);
        rowNotes.Add(small);

        Place(card, toggle, Right - 38, top + (RowH - 22) / 2, 38, 22);
        toggle.Checked = value; // before the handler, so loading doesn't count as a change
        toggle.CheckedChanged += delegate { set(); Changed(); };
        // clicking the text flips the switch too
        label.Click += delegate { toggle.Checked = !toggle.Checked; };
        small.Click += delegate { toggle.Checked = !toggle.Checked; };
    }

    static void Place(Control parent, Control control, int x, int y, int width, int height)
    {
        control.Bounds = new Rectangle(Ui.S(x), Ui.S(y), Ui.S(width), Ui.S(height));
        parent.Controls.Add(control);
    }

    // Text labels size themselves to their text, so letters like g and p are never cut off.
    static void PlaceText(Control parent, Label label, int x, int y)
    {
        label.AutoSize = true;
        label.Location = new Point(Ui.S(x), Ui.S(y));
        parent.Controls.Add(label);
    }

    const int EM_SETCUEBANNER = 0x1501; // grey hint text in an empty box
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);
}
