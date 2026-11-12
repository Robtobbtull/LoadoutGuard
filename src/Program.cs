using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// "Start with Windows": a value in the current user's Run key, the standard per-user startup list
// (no admin rights needed). The registry itself is the setting, so it always shows what is really set.
static class AutoStart
{
    public const string Argument = "--startup";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "LoadoutGuard";

    public static bool Enabled
    {
        get
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(Name) != null;
        }
        set
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (value) key.SetValue(Name, Command + " " + Argument);
                else key.DeleteValue(Name, false);
            }
        }
    }

    // Started by LoadoutGuard.ps1, this runs inside powershell.exe, so the script says how to start it again.
    static string Command
    {
        get { return Environment.GetEnvironmentVariable("LOADOUTGUARD_COMMAND") ?? "\"" + Application.ExecutablePath + "\""; }
    }

    // On by default from the first run, so nobody has to remember to start LoadoutGuard.
    // Later, if LoadoutGuard was moved since startup was turned on, point the entry at this copy.
    public static void Refresh(bool firstRun)
    {
        try
        {
            if (firstRun || Enabled) Enabled = true;
        }
        catch (Exception e)
        {
            if (!IsRegistryError(e)) throw;
        }
    }

    public static bool IsRegistryError(Exception e)
    {
        return e is UnauthorizedAccessException || e is SecurityException || e is IOException;
    }
}

static class Program
{
    public const string ShowWindowSignal = "LoadoutGuard.ShowWindow";

    [STAThread]
    static void Main(string[] args)
    {
        // Real pixels everywhere: with Windows display scaling (common at 4K) a non-aware app gets
        // scaled window positions that would not match the mouse hook's physical click positions.
        SetProcessDPIAware();
        using (var screen = Graphics.FromHwnd(IntPtr.Zero)) Ui.Scale = screen.DpiX / 96f;
        // the window must fit on small or highly scaled screens: room for it plus its title bar and a margin
        Ui.FitHeight(MainWindow.Height96, Screen.PrimaryScreen.WorkingArea.Height - SystemInformation.CaptionHeight - Ui.S(24));

        bool firstInstance;
        using (new Mutex(true, "LoadoutGuard.SingleInstance", out firstInstance))
        {
            if (!firstInstance)
            {
                // starting LoadoutGuard again opens the window of the copy that is already running
                AllowSetForegroundWindow(-1); // ASFW_ANY: let that copy bring its window to the front
                try
                {
                    using (var show = EventWaitHandle.OpenExisting(ShowWindowSignal)) show.Set();
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    MessageBox.Show("LoadoutGuard is already running. Look for its icon in the system tray.", "LoadoutGuard");
                }
                return;
            }

            Settings settings;
            Detector detector;
            ClickArea saveButton;
            try
            {
                settings = Settings.Load();
                detector = new Detector(settings.EditorOpenPattern);
                saveButton = ClickArea.Parse(settings.SaveButtonArea);
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not load the settings:\n" + e.Message + "\n\nSettings file:\n" + Settings.FilePath, "LoadoutGuard");
                return;
            }
            settings.GameLog = FindGameLog(settings.GameLog);
            AutoStart.Refresh(settings.IsNew);
            if (!RunByPowerShell) Updater.CleanUp(Application.ExecutablePath);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false); // labels draw like TextRenderer measures
            bool atSignIn = Array.IndexOf(args, AutoStart.Argument) >= 0;
            Application.Run(new TrayApp(settings, detector, saveButton, atSignIn));
        }
    }

    // Started by LoadoutGuard.ps1 (the PowerShell install), the exe is powershell.exe, which must never be touched.
    public static bool RunByPowerShell
    {
        get { return string.Equals(Path.GetFileName(Application.ExecutablePath), "powershell.exe", StringComparison.OrdinalIgnoreCase); }
    }

    // Uses the configured path if it exists, else looks for a LIVE install on every fixed drive.
    static string FindGameLog(string configured)
    {
        if (File.Exists(configured)) return configured;
        string[] subPaths =
        {
            @"Program Files\Roberts Space Industries\StarCitizen\LIVE\Game.log",
            @"Roberts Space Industries\StarCitizen\LIVE\Game.log",
            @"Games\Roberts Space Industries\StarCitizen\LIVE\Game.log",
        };
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
            foreach (var sub in subPaths)
            {
                var candidate = Path.Combine(drive.RootDirectory.FullName, sub);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return configured;
    }

    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")]
    static extern bool AllowSetForegroundWindow(int processId);
}

class TrayApp : ApplicationContext
{
    readonly Settings settings;
    readonly Detector detector;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
    readonly ReminderForm banner = new ReminderForm();
    readonly ScreenFlash flash = new ScreenFlash();
    readonly MouseWatcher mouse = new MouseWatcher();
    readonly MenuItem pauseItem;
    Popup updatePopup; // null when no update check is showing
    readonly EventWaitHandle showSignal;
    readonly RegisteredWaitHandle showWait;
    readonly SynchronizationContext ui;
    LogTail tail;
    ClickArea saveButton; // null = Save button detection off
    MainWindow window;    // null while closed

    public TrayApp(Settings settings, Detector detector, ClickArea saveButton, bool atSignIn)
    {
        this.settings = settings;
        this.detector = detector;
        this.saveButton = saveButton;
        ui = SynchronizationContext.Current; // set up by the banner form created above
        tail = new LogTail(settings.GameLog);
        banner.ApplyTheme(Theme.Find(settings.Theme));
        banner.VisibleChanged += delegate { if (!banner.Visible) flash.Stop(); }; // closing the banner ends the blinking
        mouse.GameClicked += OnGameClicked;

        var openItem = new MenuItem("Open LoadoutGuard", delegate { ShowWindow(); });
        openItem.DefaultItem = true;
        pauseItem = new MenuItem("Pause reminders", delegate { pauseItem.Checked = !pauseItem.Checked; });
        tray.ContextMenu = new ContextMenu(new[] { openItem, pauseItem, new MenuItem("-"), new MenuItem("Exit", delegate { ExitThread(); }) });
        tray.Icon = Ui.MakeIcon(Theme.Find(settings.Theme));
        tray.DoubleClick += delegate { ShowWindow(); };
        tray.Visible = true;

        showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowWindowSignal);
        showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, delegate { ui.Post(delegate { ShowWindow(); }, null); }, null, -1, false);

        poll.Interval = 1000;
        poll.Tick += delegate { Poll(); };
        Poll();
        poll.Start();

        if (settings.IsNew)
            ShowWindow();
        else if (!atSignIn || !tail.FileFound) // started with Windows: stay quiet unless something is wrong
            tray.ShowBalloonTip(5000, "LoadoutGuard is running",
                tail.FileFound
                    ? "It stays in the system tray. Double-click the icon to open it."
                    : "Game.log not found. Double-click the tray icon to set where it is.",
                tail.FileFound ? ToolTipIcon.Info : ToolTipIcon.Warning);
    }

    void Poll()
    {
        var now = DateTime.UtcNow;
        foreach (var line in tail.ReadNewLines()) detector.Check(line, now);
        UpdateMouseWatcher(now);
        tray.Text = tail.FileFound ? "LoadoutGuard" : "LoadoutGuard: Game.log not found";
        if (window != null) window.ShowStatus(tail.FileFound);
    }

    // The mouse hook only runs while the loadout editor is probably open; the rest of the time it is off.
    void UpdateMouseWatcher(DateTime now)
    {
        bool wanted = saveButton != null && !pauseItem.Checked && detector.EditorProbablyOpen(now);
        if (wanted == mouse.Running) return;
        if (!wanted)
        {
            mouse.Stop();
            return;
        }
        try
        {
            mouse.Start();
        }
        catch (Win32Exception e)
        {
            saveButton = null; // don't retry every second
            tray.ShowBalloonTip(8000, "LoadoutGuard", "Can't watch the Save button (" + e.Message + "), so no reminders can show.", ToolTipIcon.Warning);
        }
    }

    void OnGameClicked(Rectangle gameWindow, Point click)
    {
        if (saveButton == null || !saveButton.Contains(gameWindow, click)) return;
        var message = detector.SaveClicked(DateTime.UtcNow);
        if (message != null && !pauseItem.Checked) ShowBanner();
    }

    void ShowBanner()
    {
        // the player's own text replaces the standard Save reminder
        string message = settings.CustomMessage.Length > 0 ? settings.CustomMessage : Detector.LoadoutSavedMessage;
        if (settings.Flash) flash.Blink(settings.FlashSeconds); // first, so the banner shows above the red
        banner.ShowMessage(message, settings.BannerSeconds);
    }

    void ShowWindow()
    {
        if (window == null)
        {
            window = new MainWindow(settings, OnSettingsChanged, delegate { ShowBanner(); }, CheckForUpdates, Uninstall);
            window.FormClosed += delegate { window = null; };
        }
        window.ShowStatus(tail.FileFound);
        window.Show();
        if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
        window.Activate();
    }

    // Removes everything LoadoutGuard put on this PC, then exits.
    void Uninstall()
    {
        var popup = new Popup(Theme.Find(settings.Theme));
        popup.Set("Uninstall LoadoutGuard?",
            "This removes LoadoutGuard from this PC: its settings, its startup entry and the program itself. Your game is not touched.",
            "Uninstall", delegate { popup.Close(); RemoveEverything(); }, "Cancel");
    }

    void RemoveEverything()
    {
        try
        {
            AutoStart.Enabled = false;
        }
        catch (Exception e)
        {
            if (!AutoStart.IsRegistryError(e)) throw;
        }
        var paths = new List<string>
        {
            Path.GetDirectoryName(Settings.FilePath),
            // the PowerShell install (install.ps1)
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LoadoutGuard"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "LoadoutGuard.lnk"),
        };
        if (!Program.RunByPowerShell) paths.Add(Application.ExecutablePath);
        Process.Start(new ProcessStartInfo("cmd.exe", "/c " + DeleteCommand(paths)) { CreateNoWindow = true, UseShellExecute = false });
        ExitThread();
    }

    // Network and file work runs off the UI thread: the mouse hook lives there and must never stall.
    void CheckForUpdates()
    {
        if (updatePopup != null)
        {
            updatePopup.Activate(); // already checking or downloading
            return;
        }
        OpenUpdatePopup();
        StartCheck();
    }

    void OpenUpdatePopup()
    {
        updatePopup = new Popup(Theme.Find(settings.Theme));
        updatePopup.FormClosed += delegate { updatePopup = null; };
    }

    void StartCheck()
    {
        updatePopup.Set("Checking for updates...", "Asking GitHub for the latest version.", null, null, "Cancel");
        ThreadPool.QueueUserWorkItem(delegate
        {
            string tag = null, error = null;
            try
            {
                tag = Updater.LatestTag();
            }
            catch (Exception e)
            {
                error = e.Message;
            }
            ui.Post(delegate { OfferUpdate(tag, error); }, null);
        });
    }

    void OfferUpdate(string tag, string error)
    {
        if (updatePopup == null) return; // cancelled
        if (error != null)
        {
            updatePopup.Set("Couldn't check for updates", error + "\n\nCheck your internet connection and try again.", "Try again", StartCheck, "Close");
            return;
        }
        if (!Updater.IsNewer(tag, Updater.Current))
        {
            updatePopup.Set("You're up to date", "LoadoutGuard " + Updater.Current + " is the latest version.", null, null, "Close");
            return;
        }
        updatePopup.Set("Update available", "LoadoutGuard " + tag.TrimStart('v', 'V') + " is out. You have " + Updater.Current +
            ".\n\nYour settings stay. LoadoutGuard restarts by itself when the update is done.", "Update now", delegate { InstallUpdate(tag); }, "Later");
    }

    void InstallUpdate(string tag)
    {
        if (Program.RunByPowerShell)
        {
            updatePopup.Set("Updating...", "LoadoutGuard closes now and starts again in a few seconds.", null, null, null);
            // install.ps1 installs the latest release and starts it; the pause lets this copy exit first
            const string script = "Start-Sleep 3; irm https://raw.githubusercontent.com/Robtobbtull/LoadoutGuard/main/install.ps1 | iex";
            Process.Start(new ProcessStartInfo(Application.ExecutablePath, "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"" + script + "\"")
                { CreateNoWindow = true, UseShellExecute = false });
            ExitThread();
            return;
        }

        updatePopup.Set("Downloading update...", "This takes a few seconds. LoadoutGuard restarts by itself when it's done.", null, null, null);
        string exe = Application.ExecutablePath;
        ThreadPool.QueueUserWorkItem(delegate
        {
            string failed = null;
            try
            {
                Updater.ReplaceExe(tag, exe);
            }
            catch (Exception e)
            {
                failed = e.Message;
            }
            ui.Post(delegate { FinishUpdate(exe, failed); }, null);
        });
    }

    // Runs even if the popup was closed meanwhile: once the new exe is in place, LoadoutGuard must restart.
    void FinishUpdate(string exe, string failed)
    {
        if (failed != null)
        {
            if (updatePopup == null) OpenUpdatePopup(); // closed during the download: reopen it to say what went wrong
            updatePopup.Set("Couldn't update", failed + "\n\nYou can download the new LoadoutGuard.exe from the Releases page instead.",
                "Open Releases", delegate { Process.Start(Updater.ReleasesPage); updatePopup.Close(); }, "Close");
            return;
        }
        // the new copy starts once this one has exited, so it isn't stopped as a second instance
        Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1 >nul & start \"\" \"" + exe + "\"") { CreateNoWindow = true, UseShellExecute = false });
        ExitThread();
    }

    // A running exe can't delete itself, so cmd deletes the files a moment after LoadoutGuard exits.
    public static string DeleteCommand(IEnumerable<string> paths)
    {
        var command = "ping -n 3 127.0.0.1 >nul";
        foreach (var path in paths)
            command += " & rd /s /q \"" + path + "\" 2>nul & del /f /q \"" + path + "\" 2>nul";
        return command;
    }

    // The window already changed (and saved) the shared settings; apply what needs applying.
    void OnSettingsChanged()
    {
        banner.ApplyTheme(Theme.Find(settings.Theme));
        tray.Icon = Ui.MakeIcon(Theme.Find(settings.Theme));
        if (!string.Equals(tail.FilePath, settings.GameLog, StringComparison.OrdinalIgnoreCase))
            tail = new LogTail(settings.GameLog);
        Poll();
    }

    protected override void ExitThreadCore()
    {
        poll.Stop();
        mouse.Dispose();
        if (window != null) window.Close();
        showWait.Unregister(null);
        showSignal.Dispose();
        tray.Visible = false; // otherwise a dead icon stays in the tray until hovered
        tray.Dispose();
        banner.Dispose();
        flash.Dispose();
        base.ExitThreadCore();
    }
}
