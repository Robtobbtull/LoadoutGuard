using System;
using System.Drawing;

// Colour palettes for the window and the banner: calm surfaces, soft text, one accent.
// Line colours are tints mixed onto the background, so they stay solid colours.
class Theme
{
    public readonly string Name;
    public readonly bool Dark;
    public readonly Color Bg, Side, Surface, Surface2, Line, Line2, Text, Text2, Text3, Accent, Accent2, AccentInk, Danger;

    Theme(string name, bool dark, int bg, int side, int surface, int surface2, int tint, double line, double line2,
          int text, int text2, int text3, int accent, int accent2, int accentInk, int danger)
    {
        Name = name;
        Dark = dark;
        Bg = Rgb(bg);
        Side = Rgb(side);
        Surface = Rgb(surface);
        Surface2 = Rgb(surface2);
        Line = Mix(Bg, Rgb(tint), line);
        Line2 = Mix(Bg, Rgb(tint), line2);
        Text = Rgb(text);
        Text2 = Rgb(text2);
        Text3 = Rgb(text3);
        Accent = Rgb(accent);
        Accent2 = Rgb(accent2);
        AccentInk = Rgb(accentInk);
        Danger = Rgb(danger);
    }

    // The first theme is the default.
    public static readonly Theme[] All =
    {
        new Theme("Monochrome", true, 0x111111, 0x0b0b0b, 0x1a1a1a, 0x232323, 0xffffff, 0.09, 0.18,
            0xf2f2f2, 0xb8b8b8, 0x7a7a7a, 0xf2f2f2, 0xffffff, 0x111111, 0xff7b72),
        new Theme("Nebula", true, 0x16171c, 0x111216, 0x1f2128, 0x282a33, 0xc8cdff, 0.09, 0.17,
            0xe9e9f1, 0xb4b5c4, 0x7d7f8f, 0x9d8cff, 0xb2a5ff, 0x15131f, 0xf07b84),
        new Theme("Ember", true, 0x262624, 0x1f1e1d, 0x30302e, 0x383835, 0xdedcd1, 0.10, 0.18,
            0xf0eee6, 0xc3c0b6, 0x8f8d86, 0xd97757, 0xe08a6d, 0x1f1e1d, 0xe5786a),
        new Theme("Midnight", true, 0x0e1420, 0x0a0f18, 0x162033, 0x1d2a42, 0xaac8ff, 0.10, 0.18,
            0xe6edf7, 0xa9b8cc, 0x6f8098, 0x5fb3f0, 0x7cc3f5, 0x06121f, 0xf2837a),
        new Theme("mobiGlas", true, 0x0b131b, 0x070d13, 0x122030, 0x182a3d, 0x78d2ff, 0.10, 0.20,
            0xe3f3fb, 0xa7c3d3, 0x6d8899, 0x35d0ff, 0x6bdcff, 0x04121b, 0xf2837a),
        new Theme("Paper", false, 0xf4f1ea, 0xebe7dd, 0xfffdf8, 0xf0ece3, 0x3c321e, 0.11, 0.20,
            0x2a2824, 0x55514a, 0x8a857b, 0xc4572f, 0xb14b26, 0xfffdf8, 0xc0392b),
    };

    // Unknown names fall back to the default theme.
    public static Theme Find(string name)
    {
        foreach (var t in All)
            if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)) return t;
        return All[0];
    }

    public static Color Mix(Color a, Color b, double amount)
    {
        return Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * amount),
            (int)Math.Round(a.G + (b.G - a.G) * amount),
            (int)Math.Round(a.B + (b.B - a.B) * amount));
    }

    static Color Rgb(int rgb)
    {
        return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
    }
}
