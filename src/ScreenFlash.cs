using System;
using System.Drawing;
using System.Windows.Forms;

// A light red blink over the screen the game is on, with each reminder: soft pulses, one a second,
// that stay see-through, so the game is always visible. Clicks pass straight through to the game,
// and it never takes focus.
class ScreenFlash : Form
{
    const int PulseMs = 1000;
    const double Peak = 0.22; // the strongest the red gets
    readonly Timer timer = new Timer { Interval = 15 };
    DateTime started;
    int seconds;

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
    public void Blink(int seconds)
    {
        this.seconds = seconds;
        Bounds = Ui.ActiveScreen().Bounds;
        started = DateTime.UtcNow;
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
        Opacity = Peak * s * s;
    }
}
