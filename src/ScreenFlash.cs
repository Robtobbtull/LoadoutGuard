using System;
using System.Drawing;
using System.Windows.Forms;

// A light red blink over the screen the game is on, with each reminder: soft pulses, one a second,
// that stay see-through, so the game is always visible. Clicks pass straight through to the game,
// and it never takes focus.
class ScreenFlash : Form
{
    const int PulseMs = 1000;
    // Strongest opacity of the red for strength 1 to 5. Small steps: even a little more red is a lot
    // stronger over a dark game, and 1 is meant to be barely there.
    static readonly double[] Peaks = { 0.044, 0.057, 0.073, 0.092, 0.12 };
    readonly Timer timer = new Timer { Interval = 15 };
    DateTime started;
    int seconds;
    double peak;
    double carry; // the part of an alpha step not shown yet, see Step()

    public ScreenFlash()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(230, 20, 20);
        Opacity = 0;
        timer.Tick += delegate { Step(); };
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            // WS_EX_TRANSPARENT with WS_EX_LAYERED: mouse clicks go to the window below (the game)
            const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
                WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    // Blinks for this many seconds, or with 0 until Stop() is called (when the banner closes).
    // strength: 1 (most subtle) to 5.
    public void Blink(int seconds, int strength)
    {
        this.seconds = seconds;
        peak = Peaks[Math.Max(1, Math.Min(Peaks.Length, strength)) - 1];
        Bounds = Ui.ActiveScreen().Bounds;
        started = DateTime.UtcNow;
        carry = 0;
        Opacity = 0;
        if (!Visible) Show();
        timer.Start();
    }

    public void Stop()
    {
        timer.Stop();
        Hide();
    }

    void Step()
    {
        double ms = (DateTime.UtcNow - started).TotalMilliseconds;
        if (seconds > 0 && ms >= seconds * 1000.0)
        {
            Stop();
            return;
        }
        double s = Math.Sin(Math.PI * (ms % PulseMs) / PulseMs); // smooth in and out, no hard flicker
        // A window's alpha has only 256 steps, so a weak flash (peak alpha ~11 at 20 %) would ramp in
        // visible jumps. Carrying the remainder to the next frame alternates between neighbouring steps,
        // which averages out to the exact level, so the fade stays smooth at any strength.
        double alpha = peak * s * s * 255 + carry;
        double step = Math.Floor(alpha);
        carry = alpha - step;
        Opacity = (step + 0.5) / 255; // + 0.5: WinForms truncates Opacity * 255 to a byte
    }
}
