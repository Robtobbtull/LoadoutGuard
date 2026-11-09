using System;
using System.Drawing;
using System.Globalization;

// An area of the game's 16:9 menu area given as fractions of its size (0 = left/top edge,
// 1 = right/bottom), so one setting fits 1080p, 1440p and 4K, and also ultrawide and 16:10.
class ClickArea
{
    readonly double left, top, right, bottom;

    ClickArea(double left, double top, double right, double bottom)
    {
        this.left = left;
        this.top = top;
        this.right = right;
        this.bottom = bottom;
    }

    // "left,top,right,bottom", e.g. "0.754,0.799,0.904,0.856". Empty = no area (returns null).
    // Throws FormatException for anything else.
    public static ClickArea Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var parts = text.Split(',');
        if (parts.Length != 4) throw new FormatException("saveButtonArea needs 4 numbers: left,top,right,bottom");
        var v = new double[4];
        for (int i = 0; i < 4; i++)
        {
            // invariant culture: "0.75" must also work on PCs that use a decimal comma
            if (!double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]) || v[i] < 0 || v[i] > 1)
                throw new FormatException("saveButtonArea numbers must be between 0 and 1, with a dot as decimal point");
        }
        if (v[0] >= v[2] || v[1] >= v[3]) throw new FormatException("saveButtonArea: left must be less than right, top less than bottom");
        return new ClickArea(v[0], v[1], v[2], v[3]);
    }

    // window is the game's client area. Star Citizen draws its menus, the loadout editor included, in
    // the largest 16:9 area centred in the window: black bars at the sides on 21:9 and 32:9, above
    // and below on 16:10. So the window's aspect ratio is detected and the click measured in that area.
    public bool Contains(Rectangle window, Point click)
    {
        if (window.Width <= 0 || window.Height <= 0) return false;
        double width = Math.Min(window.Width, window.Height * 16.0 / 9);
        double height = Math.Min(window.Height, window.Width * 9.0 / 16);
        double x = (click.X - window.Left - (window.Width - width) / 2) / width;
        double y = (click.Y - window.Top - (window.Height - height) / 2) / height;
        return x >= left && x <= right && y >= top && y <= bottom;
    }
}
