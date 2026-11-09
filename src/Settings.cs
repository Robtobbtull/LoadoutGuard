using System;
using System.Collections.Generic;
using System.IO;

// settings.ini in %APPDATA%\LoadoutGuard. The settings window writes it; plain key=value so
// players can also fix the log pattern themselves when a game patch changes the log lines.
class Settings
{
    public string GameLog = @"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE\Game.log";
    // Pattern checked against Game.log from Star Citizen build 12913470 (November 2026).
    public string EditorOpenPattern = @"<UI - LoadoutEditor> .*(InitLoadout|OnLoadoutQueriesFinished)";
    // "SAVE CHANGES AND EQUIP" in the Vehicle Loadout Manager, measured at 2560x1440 with a small
    // margin, as fractions of the game's 16:9 menu area: left,top,right,bottom. Same for any
    // resolution; on other aspect ratios ClickArea finds that 16:9 area in the window.
    public string SaveButtonArea = "0.754,0.799,0.904,0.856";
    public int BannerSeconds = 20;
    public string CustomMessage = ""; // empty = the standard Save reminder text
    public bool Flash = true; // a light red blink over the game with each reminder
    public int FlashSeconds = 3; // 0 = until the banner closes
    public string Theme = "Monochrome";

    // True when no settings file existed yet (first run).
    public bool IsNew;

    public static string FilePath
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LoadoutGuard", "settings.ini");
        }
    }

    public static Settings Load()
    {
        if (File.Exists(FilePath)) return Parse(File.ReadAllLines(FilePath));
        var defaults = new Settings();
        defaults.IsNew = true;
        defaults.Save(FilePath);
        return defaults;
    }

    // Unknown keys are ignored.
    public static Settings Parse(IEnumerable<string> lines)
    {
        var s = new Settings();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            int eq = line.IndexOf('=');
            if (eq < 1) continue;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string value = line.Substring(eq + 1).Trim();
            switch (key)
            {
                case "gamelog": s.GameLog = value; break;
                case "editoropenpattern": s.EditorOpenPattern = value; break;
                case "savebuttonarea": s.SaveButtonArea = value; break;
                case "bannerseconds": s.BannerSeconds = Number(value, 1, s.BannerSeconds); break;
                case "custommessage": s.CustomMessage = value; break;
                case "flash": s.Flash = Bool(value, s.Flash); break;
                case "flashseconds": s.FlashSeconds = Number(value, 0, s.FlashSeconds); break;
                case "theme": s.Theme = value; break;
            }
        }
        return s;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllLines(path, new[]
        {
            "; LoadoutGuard settings. The LoadoutGuard window writes this file when you change something there.",
            "; To edit it by hand, exit LoadoutGuard first (tray icon > Exit), then start it again afterwards.",
            "",
            "; Full path to Star Citizen's Game.log. Use the PTU folder instead of LIVE to watch the PTU.",
            "gameLog=" + GameLog,
            "",
            "; How long the reminder banner stays on screen, in seconds. Click it to close it sooner.",
            "bannerSeconds=" + BannerSeconds,
            "; Your own text for the Save reminder banner. Leave it empty for the standard message.",
            // one line only: a line break would end the value and start a bogus setting
            "customMessage=" + CustomMessage.Replace("\r", "").Replace("\n", " "),
            "; Blink the screen lightly red with each reminder (true or false). The game stays visible.",
            "flash=" + Text(Flash),
            "; How long it blinks, in seconds (one blink per second). 0 = until the banner closes.",
            "flashSeconds=" + FlashSeconds,
            "; Monochrome, Nebula, Ember, Midnight, mobiGlas or Paper.",
            "theme=" + Theme,
            "",
            "; Advanced. Regular expression for the Game.log line written when the loadout editor opens,",
            "; which starts watching for Save clicks. Update it if a game patch changes that line.",
            "editorOpenPattern=" + EditorOpenPattern,
            "",
            "; Advanced. Where the SAVE CHANGES AND EQUIP button is: left,top,right,bottom as fractions of the",
            "; game's 16:9 menu area (0 = left/top edge, 1 = right/bottom edge). Fits any resolution; on",
            "; ultrawide or 16:10 the 16:9 area inside the black bars is found by itself. Empty = off.",
            "saveButtonArea=" + SaveButtonArea,
        });
    }

    static string Text(bool value)
    {
        return value ? "true" : "false";
    }

    static bool Bool(string value, bool fallback)
    {
        bool b;
        return bool.TryParse(value, out b) ? b : fallback;
    }

    static int Number(string value, int min, int fallback)
    {
        int n;
        return int.TryParse(value, out n) && n >= min ? n : fallback;
    }
}
