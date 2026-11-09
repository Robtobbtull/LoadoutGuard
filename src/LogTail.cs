using System.Collections.Generic;
using System.IO;
using System.Text;

// Reads lines appended to Game.log since the last call. Opens the file fresh each poll
// so it never locks the game's log and notices when a new game session replaces it.
class LogTail
{
    readonly string path;
    readonly byte[] buffer = new byte[64 * 1024];
    readonly char[] chars = new char[Encoding.UTF8.GetMaxCharCount(64 * 1024)];
    readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    readonly StringBuilder partial = new StringBuilder();
    long position = -1; // -1 = not opened yet: skip whatever history the file already has

    public bool FileFound { get; private set; }

    public string FilePath
    {
        get { return path; }
    }

    public LogTail(string path)
    {
        this.path = path;
    }

    public List<string> ReadNewLines()
    {
        var lines = new List<string>();
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException) { Missing(); return lines; }
        catch (DirectoryNotFoundException) { Missing(); return lines; }
        catch (IOException) { return lines; } // file busy for a moment (game moving it); retry next poll
        catch (System.UnauthorizedAccessException) { return lines; }

        using (stream)
        {
            FileFound = true;
            long length = stream.Length;
            if (position < 0) position = length;
            // shortcut: a shrunk file means the game started a new session. If the new log ever
            // outgrows the old one between two polls we skip its first lines; those are startup
            // lines with no loadout events, so only upgrade (e.g. compare first bytes) if that changes.
            if (length < position) Reset();
            if (length == position) return lines;

            stream.Seek(position, SeekOrigin.Begin);
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                position += read;
                int count = decoder.GetChars(buffer, 0, read, chars, 0);
                for (int i = 0; i < count; i++)
                {
                    if (chars[i] == '\n')
                    {
                        lines.Add(partial.ToString().TrimEnd('\r'));
                        partial.Clear();
                    }
                    else
                    {
                        partial.Append(chars[i]);
                    }
                }
            }
        }
        return lines;
    }

    // While the game is closed or restarting there is no Game.log; the next one is a new session.
    void Missing()
    {
        FileFound = false;
        Reset();
    }

    void Reset()
    {
        position = 0;
        partial.Clear();
        decoder.Reset();
    }
}
