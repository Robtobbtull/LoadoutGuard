using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

// Sees left clicks in the Star Citizen window. Only runs while the loadout editor is probably open.
// A low-level mouse hook runs inside LoadoutGuard itself (nothing is injected into the game) and
// only observes: every click still goes to the game unchanged.
class MouseWatcher : IDisposable
{
    // game window rectangle and click position, both in physical screen pixels
    public event Action<Rectangle, Point> GameClicked;

    readonly HookProc proc; // kept in a field so the garbage collector can't free it while hooked
    IntPtr hook = IntPtr.Zero;
    SynchronizationContext ui;

    public MouseWatcher()
    {
        proc = OnHook;
    }

    public bool Running
    {
        get { return hook != IntPtr.Zero; }
    }

    // Call from the UI thread.
    public void Start()
    {
        if (Running) return;
        ui = SynchronizationContext.Current;
        hook = SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception();
    }

    public void Stop()
    {
        if (!Running) return;
        UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
    }

    IntPtr OnHook(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message == (IntPtr)WM_LBUTTONDOWN)
        {
            var info = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(data, typeof(MSLLHOOKSTRUCT));
            var click = new Point(info.x, info.y);
            // return at once: Windows silently removes hooks that are slow
            ui.Post(delegate { Handle(click); }, null);
        }
        return CallNextHookEx(hook, code, message, data);
    }

    void Handle(Point click)
    {
        IntPtr window = GetForegroundWindow();
        uint pid;
        GetWindowThreadProcessId(window, out pid);
        try
        {
            using (var p = Process.GetProcessById((int)pid))
                if (!p.ProcessName.Equals("StarCitizen", StringComparison.OrdinalIgnoreCase)) return;
        }
        catch (ArgumentException) { return; } // process already gone

        // the client area: in windowed mode the title bar and borders are not part of the game picture
        RECT r;
        var corner = new Point(0, 0);
        if (!GetClientRect(window, out r) || !ClientToScreen(window, ref corner)) return;
        var handler = GameClicked;
        if (handler != null) handler(new Rectangle(corner.X, corner.Y, r.Right, r.Bottom), click);
    }

    const int WH_MOUSE_LL = 14;
    const int WM_LBUTTONDOWN = 0x0201;

    delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    struct MSLLHOOKSTRUCT { public int x, y; public uint mouseData, flags, time; public IntPtr extraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, HookProc proc, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")]
    static extern bool ClientToScreen(IntPtr window, ref Point point);
}
