using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

// SelfTest.exe            runs the self-tests (build.cmd does this).
// SelfTest.exe <log file>  replays a Game.log and prints when the loadout editor opened.
//                          Use it to check the editor pattern after a game patch.
static class SelfTest
{
    // Game.log lines from Star Citizen build 12913470, from a session where a loadout was saved
    // (some shortened, the vehicle editor line rebuilt from an older log, player name replaced).
    const string EditorOpenLine = "<2026-11-08T00:42:55.867Z> [Notice] <UI - LoadoutEditor> CLoadoutDummy::InitLoadout LocalEntitySpawnBatch took 0.176727 seconds [Team_UI][Loadout]";
    const string VehicleEditorLine = "<2026-10-24T21:30:00.000Z> [Notice] <UI - LoadoutEditor> CVehicleLoadoutEditorContextComponent::OnLoadoutQueriesFinished LocalEntitySpawnBatch took 0.2 seconds [Team_UI][Loadout]";
    const string EditorDummyAtcLine = "<2026-11-08T00:42:55.866Z> [Notice] <ATC_Reset_VehiclesBeingStoredCleared> ATC 'Default_2030' [2030] clearing 0 entries from vehicles-being-stored list on Reset [Team_MissionFeatures][ATC]";
    const string ShipReloadLine = "<2026-11-08T00:44:05.580Z> [Notice] <ATC_Reset_VehiclesBeingStoredCleared> ATC 'CNOU_Mustang_ShipATCDataManager_917614485258' [917614485258] clearing 0 entries from vehicles-being-stored list on Reset [Team_MissionFeatures][ATC]";
    const string DuplicatePortLine = "<2026-11-08T00:39:35.458Z> [Error] <DuplicatedItemPorts> Error in entities passed to creation batch 'CLoadoutDummy::InitLoadout'. Found multiple entities attempting to attach to the same parent item port 1383588930 [Team_EntitySystemTech]";
    const string ShipSpawningLine = "<2026-10-14T19:22:16.145Z> [Notice] <CEntityComponentShipListProvider::SetVehicleSpawningInformations> SetVehicleSpawningInformations - VehicleEntityId: [881183460316], LandingArea: PLAYER's";
    const string VehicleListLine ="<2026-11-08T00:45:00.982Z> [Notice] <OnRequestFetchVehicles> Fetching player vehicle list [Team_GameServices][ASOP][Entitlement][Insurance]";

    static int failures;

    static int Main(string[] args)
    {
        if (args.Length == 1) return Replay(args[0]);

        TestLogTail();
        TestDetector();
        TestClickArea();
        TestSettings();
        Console.WriteLine(failures == 0 ? "All self-tests passed." : failures + " self-test(s) failed.");
        return failures == 0 ? 0 : 1;
    }

    static void TestLogTail()
    {
        string path = Path.Combine(Path.GetTempPath(), "LoadoutGuard-selftest-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            File.WriteAllText(path, "old line\n");
            var tail = new LogTail(path);
            Check(tail.ReadNewLines().Count == 0 && tail.FileFound, "history already in the file is skipped at start");
            File.AppendAllText(path, "a\nb");
            Check(Same(tail.ReadNewLines(), "a"), "a complete line is returned, an unfinished one is held back");
            File.AppendAllText(path, "c\r\n");
            Check(Same(tail.ReadNewLines(), "bc"), "the held-back part is joined and CRLF is stripped");
            Check(tail.ReadNewLines().Count == 0, "no new data gives no lines");
            File.WriteAllText(path, "new session\n");
            Check(Same(tail.ReadNewLines(), "new session"), "a shrunk file (new game session) is read from the start");
            File.Delete(path);
            Check(tail.ReadNewLines().Count == 0 && !tail.FileFound, "a missing file is reported");
            File.WriteAllText(path, "x\n");
            Check(Same(tail.ReadNewLines(), "x"), "a recreated file is read from the start");
        }
        finally
        {
            File.Delete(path);
        }
    }

    static void TestDetector()
    {
        var s = new Settings();
        var d = new Detector(s.EditorOpenPattern);
        var t = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);

        foreach (var other in new[] { ShipReloadLine, DuplicatePortLine, EditorDummyAtcLine, VehicleListLine, ShipSpawningLine })
            Check(!d.Check(other, t), "not the editor opening: " + other.Substring(36, 40));
        Check(!d.EditorProbablyOpen(t), "other lines don't start watching for Save clicks");

        Check(d.Check(EditorOpenLine, t), "the editor line is seen");
        Check(d.Check(VehicleEditorLine, t.AddSeconds(60)), "the vehicle editor line is seen");

        var off = new Detector("");
        Check(!off.Check(EditorOpenLine, t) && !off.EditorProbablyOpen(t), "without an editor pattern Save detection never starts");

        // Save button clicks
        var save = new Detector(s.EditorOpenPattern);
        Check(save.SaveClicked(t) == null && !save.EditorProbablyOpen(t), "a Save-area click with no editor open is ignored");
        save.Check(EditorOpenLine, t);
        Check(save.EditorProbablyOpen(t.AddMinutes(1)), "the editor counts as open after its log line");
        Check(save.SaveClicked(t.AddSeconds(60)) == Detector.LoadoutSavedMessage, "clicking Save in the editor reminds");
        Check(save.SaveClicked(t.AddSeconds(62)) == null, "a double click is one save");
        Check(save.SaveClicked(t.AddSeconds(120)) == Detector.LoadoutSavedMessage, "saving again later reminds again");
        var late = t + Detector.EditorOpenFor + TimeSpan.FromSeconds(1);
        Check(!save.EditorProbablyOpen(late) && save.SaveClicked(late) == null, "clicks long after the editor opened are ignored");
    }

    static void TestClickArea()
    {
        var area = ClickArea.Parse(new Settings().SaveButtonArea);

        // Button corners measured on a 2560x1440 screenshot: (1940,1160) to (2305,1222).
        // The same spot scaled to other 16:9 resolutions must be inside; nearby controls must not.
        foreach (var size in new[] { new Size(1920, 1080), new Size(2560, 1440), new Size(3840, 2160) })
        {
            var window = new Rectangle(0, 0, size.Width, size.Height);
            string res = size.Width + "x" + size.Height + ": ";
            Check(area.Contains(window, At(size, 1940, 1162)), res + "button top-left corner");
            Check(area.Contains(window, At(size, 2300, 1220)), res + "button bottom-right corner");
            Check(area.Contains(window, At(size, 2120, 1192)), res + "button middle");
            Check(!area.Contains(window, At(size, 1700, 1192)), res + "left of the button");
            Check(!area.Contains(window, At(size, 2190, 1320)), res + "mobiGlas VEHICLES button below");
            Check(!area.Contains(window, At(size, 2120, 1000)), res + "ship view above");
            Check(!area.Contains(window, At(size, 2350, 105)), res + "close (X) button");
        }

        // other aspect ratios: the game draws the editor in a centred 16:9 area with black bars around it
        var ultrawide = new Rectangle(0, 0, 3440, 1440); // 21:9: 440 px bars left and right
        Check(area.Contains(ultrawide, new Point(2120 + 440, 1192)), "21:9: button middle, shifted by the side bar");
        Check(!area.Contains(ultrawide, new Point(3440 * 2120 / 2560, 1192)), "21:9: the stretched position misses");
        var superwide = new Rectangle(0, 0, 5120, 1440); // 32:9: 1280 px bars
        Check(area.Contains(superwide, new Point(2120 + 1280, 1192)), "32:9: button middle");
        Check(!area.Contains(superwide, new Point(2120, 1192)), "32:9: the 16:9 position without the bar misses");
        var tall = new Rectangle(0, 0, 1920, 1200); // 16:10: 60 px bars above and below
        var tallMid = At(new Size(1920, 1080), 2120, 1192);
        Check(area.Contains(tall, new Point(tallMid.X, tallMid.Y + 60)), "16:10: button middle, shifted by the top bar");
        Check(!area.Contains(tall, At(new Size(1920, 1200), 2190, 1320)), "16:10: mobiGlas VEHICLES button below");

        // a game window that does not start at the screen corner (windowed mode)
        var moved = new Rectangle(100, 50, 1920, 1080);
        var mid = At(new Size(1920, 1080), 2120, 1192);
        Check(area.Contains(moved, new Point(mid.X + 100, mid.Y + 50)), "windowed game: window offset is applied");
        Check(!area.Contains(moved, mid), "windowed game: unshifted point is outside");

        Check(ClickArea.Parse("") == null, "empty area = off");
        foreach (var bad in new[] { "0.1,0.2,0.3", "0,754;0,799;0,904;0,856", "0.9,0.8,0.7,0.85", "0.1,0.2,1.5,0.3" })
        {
            bool threw = false;
            try { ClickArea.Parse(bad); } catch (FormatException) { threw = true; }
            Check(threw, "rejects saveButtonArea=" + bad);
        }
    }

    // Converts a pixel position on the 2560x1440 screenshot to the same spot at another resolution.
    static Point At(Size size, int x1440p, int y1440p)
    {
        return new Point((int)Math.Round(x1440p * size.Width / 2560.0), (int)Math.Round(y1440p * size.Height / 1440.0));
    }

    static void TestSettings()
    {
        var p = Settings.Parse(new[] { "; comment", "editorOpenPattern = a=b", "saveButtonArea=", "bannerSeconds=abc", "FLASH=false", "flashSeconds=-2", "unknown=true", "junk" });
        Check(p.EditorOpenPattern == "a=b", "a value may contain '='");
        Check(p.SaveButtonArea == "", "an empty value is kept (turns Save detection off)");
        Check(p.BannerSeconds == new Settings().BannerSeconds, "a bad number keeps the default");
        Check(p.FlashSeconds == new Settings().FlashSeconds, "a negative flash time keeps the default");
        Check(!p.Flash, "booleans and key case are handled; unknown keys are ignored");
        Check(Settings.Parse(new[] { "flash=maybe" }).Flash, "a bad boolean keeps the default");
        Check(!p.IsNew, "a parsed file is not a first run");

        Check(Theme.Find("paper").Name == "Paper", "theme names ignore case");
        Check(Theme.Find(new Settings().Theme) == Theme.All[0], "the default theme is the first one");
        Check(Theme.All[0].Name == "Monochrome", "Monochrome is the default theme");
        var fresh = new Settings();
        Check(fresh.Flash && fresh.FlashSeconds == 3, "defaults: the red flash is on, for 3 seconds");
        Check(Theme.Find("nope") == Theme.All[0] && Theme.Find(null) == Theme.All[0], "an unknown theme falls back to the first");

        string path = Path.Combine(Path.GetTempPath(), "LoadoutGuard-selftest-" + Guid.NewGuid().ToString("N") + ".ini");
        try
        {
            var original = new Settings();
            original.GameLog = @"D:\Games\StarCitizen\PTU\Game.log";
            original.BannerSeconds = 7;
            original.Flash = false;
            original.FlashSeconds = 0;
            original.Theme = "Ember";
            original.SaveButtonArea = "0.1,0.2,0.3,0.4";
            original.CustomMessage = "Insure the ship = do it now!";
            original.Save(path);
            var back = Settings.Parse(File.ReadAllLines(path));
            Check(back.CustomMessage == original.CustomMessage, "a custom message reads back unchanged, '=' included");
            original.CustomMessage = "Save\r\ninsurance";
            original.Save(path);
            Check(Settings.Parse(File.ReadAllLines(path)).CustomMessage == "Save insurance", "a line break in the custom message is saved as a space");
            Check(new Settings().CustomMessage == "", "no custom message by default");
            Check(back.GameLog == original.GameLog && back.EditorOpenPattern == original.EditorOpenPattern
                && back.BannerSeconds == 7 && !back.Flash && back.FlashSeconds == 0 && back.Theme == "Ember"
                && back.SaveButtonArea == "0.1,0.2,0.3,0.4",
                "saved settings read back unchanged");
        }
        finally
        {
            File.Delete(path);
        }
    }

    static int Replay(string path)
    {
        var d = new Detector(new Settings().EditorOpenPattern);
        var timestamp = new Regex(@"^<(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(\.\d+)?Z)>");
        var now = DateTime.MinValue;
        int opened = 0;
        using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var m = timestamp.Match(line);
                if (m.Success) now = DateTime.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
                if (!d.Check(line, now)) continue;
                opened++;
                Console.WriteLine("{0:yyyy-MM-dd HH:mm:ss} UTC  loadout editor opened: Save clicks watched", now);
            }
        }
        Console.WriteLine("{0} time(s) the loadout editor opened.", opened);
        return 0;
    }

    static bool Same(List<string> lines, params string[] expected)
    {
        if (lines.Count != expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
            if (lines[i] != expected[i]) return false;
        return true;
    }

    static void Check(bool ok, string what)
    {
        if (ok) return;
        failures++;
        Console.WriteLine("FAIL: " + what);
    }
}
