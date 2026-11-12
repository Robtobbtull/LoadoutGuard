using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

// "Check for updates" in the window: the only time LoadoutGuard goes online, and only when the player clicks it.
static class Updater
{
    public const string Current = "1.0.1"; // must match the release tag without its "v"; CI checks this on tags
    const string Repo = "Robtobbtull/LoadoutGuard";
    public const string ReleasesPage = "https://github.com/" + Repo + "/releases";

    // The latest release's tag, such as "v1.0.2".
    public static string LatestTag()
    {
        using (var web = Client())
        {
            var json = web.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");
            var m = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!m.Success) throw new WebException("GitHub sent no release version.");
            return m.Groups[1].Value;
        }
    }

    // Also the gate for using the tag in a download URL: only a plain version number passes.
    public static bool IsNewer(string tag, string current)
    {
        Version latest, mine;
        return tag != null && Version.TryParse(tag.TrimStart('v', 'V'), out latest)
            && Version.TryParse(current, out mine) && latest > mine;
    }

    // Downloads the release's exe next to the running one and swaps them.
    // A running exe can be renamed but not overwritten, so it becomes .old until the next start.
    public static void ReplaceExe(string tag, string exe)
    {
        string fresh = exe + ".new", old = exe + ".old";
        using (var web = Client())
            web.DownloadFile("https://github.com/" + Repo + "/releases/download/" + tag + "/LoadoutGuard.exe", fresh);
        if (!IsProgram(fresh))
        {
            File.Delete(fresh);
            throw new IOException("The download is not a program file.");
        }
        File.Delete(old);
        File.Move(exe, old);
        try
        {
            File.Move(fresh, exe);
        }
        catch
        {
            File.Move(old, exe); // put the working copy back
            throw;
        }
    }

    // Removes what an update left behind: the previous exe, or a download that never got swapped in.
    public static void CleanUp(string exe)
    {
        foreach (var path in new[] { exe + ".old", exe + ".new" })
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is UnauthorizedAccessException)) throw;
            }
        }
    }

    static bool IsProgram(string path)
    {
        var start = new byte[2];
        using (var file = File.OpenRead(path))
            return file.Read(start, 0, 2) == 2 && start[0] == 'M' && start[1] == 'Z';
    }

    static WebClient Client()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; // the default can be TLS 1.0, which GitHub refuses
        var web = new WebClient();
        web.Headers[HttpRequestHeader.UserAgent] = "LoadoutGuard"; // GitHub's API refuses requests without one
        return web;
    }
}
