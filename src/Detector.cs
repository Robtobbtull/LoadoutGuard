using System;
using System.Text.RegularExpressions;

// Decides when to remind the player. Pure logic so SelfTest can drive it.
// Game.log has no "loadout saved" or "insurance saved" line (checked on build 12913470). Reliable
// signals: the loadout editor opening (a log line), and a click on the Save button while the editor
// is open (seen by MouseWatcher, checked against ClickArea).
class Detector
{
    public const string LoadoutSavedMessage =
        "Loadout saved.\nYour insurance is NOT updated. Save insurance at the MFD now.";

    // The game logs no line when the editor closes, so treat it as open this long after it opened.
    public static readonly TimeSpan EditorOpenFor = TimeSpan.FromMinutes(15);
    static readonly TimeSpan SameSave = TimeSpan.FromSeconds(10); // a double click on Save is one save

    readonly Regex editorOpened;
    DateTime editorOpenUntil = DateTime.MinValue, lastSave = DateTime.MinValue;

    // An empty pattern turns Save detection off. Throws ArgumentException for an invalid pattern.
    public Detector(string editorOpenPattern)
    {
        editorOpened = string.IsNullOrEmpty(editorOpenPattern) ? null : new Regex(editorOpenPattern, RegexOptions.CultureInvariant);
    }

    public bool EditorProbablyOpen(DateTime now)
    {
        return now < editorOpenUntil;
    }

    // Call for each new Game.log line. Returns true when the line shows the loadout editor opening.
    public bool Check(string line, DateTime now)
    {
        if (editorOpened == null || !editorOpened.IsMatch(line)) return false;
        editorOpenUntil = now + EditorOpenFor;
        return true;
    }

    // Call when the player clicked inside the Save button area of the game window.
    // Returns the reminder text, or null for no reminder.
    public string SaveClicked(DateTime now)
    {
        if (!EditorProbablyOpen(now)) return null;
        bool sameSave = now - lastSave < SameSave;
        lastSave = now;
        return sameSave ? null : LoadoutSavedMessage;
    }
}
