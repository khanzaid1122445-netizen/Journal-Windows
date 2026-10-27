using System;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Threading;

namespace Journal;

/// <summary>
/// Global hotkeys via Win32 RegisterHotKey, on a hidden message-only window.
/// Windows only; on any other OS this is a no-op and Start() returns false.
/// Failures are reported per shortcut (CaptureError / EveningError), never
/// as one generic message, and a failed hotkey never blocks tray use:
/// the app still works fully from the tray.
/// </summary>
public static class HotkeyService
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    private const int WM_HOTKEY = 0x0312;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static readonly WndProc Proc = HandleMessage;
    private static IntPtr _hwnd;
    private static Thread? _thread;
    private static readonly ManualResetEventSlim _registered = new(false);
    private static string _captureShortcut = "";
    private static string _eveningShortcut = "";

    public static Action? OnCapture { get; set; }
    public static Action? OnEvening { get; set; }

    /// <summary>Per-shortcut failure, or "" when that shortcut registered OK.</summary>
    public static string CaptureError { get; private set; } = "";
    public static string EveningError { get; private set; } = "";

    /// <summary>Combined failure text, kept for callers that only show one line.</summary>
    public static string LastError => CaptureError != "" ? CaptureError : EveningError;

    public static bool AnyError => CaptureError != "" || EveningError != "";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int w, int h, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASS
    {
        public uint style; public IntPtr lpfnWndProc; public int cbClsExtra; public int cbWndExtra;
        public IntPtr hInstance; public IntPtr hIcon; public IntPtr hCursor; public IntPtr hbrBackground;
        public string? lpszMenuName; public string lpszClassName;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam;
        public uint time; public int ptX; public int ptY;
    }

    private static IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            var action = id == 1 ? OnCapture : id == 2 ? OnEvening : null;
            if (action != null) Dispatcher.UIThread.Post(action);
            return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public static bool TryParse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0; vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return false;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToLowerInvariant())
            {
                case "ctrl": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": modifiers |= MOD_SHIFT; break;
                case "win": modifiers |= MOD_WIN; break;
                default: return false;
            }
        }
        var key = parts[^1];
        if (key.Length == 1)
        {
            vk = char.ToUpperInvariant(key[0]);
            return vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A;
        }
        return false;
    }

    public static bool IsBanned(string text)
    {
        var t = text.Replace(" ", "").ToLowerInvariant();
        return t is "ctrl+shift+space" or "ctrl+shift+e" or "ctrl+shift+esc";
    }

    private static string RegisterOne(IntPtr hwnd, int id, string shortcut, string label)
    {
        if (!TryParse(shortcut, out var m, out var v))
            return $"{label} shortcut '{shortcut}' could not be understood. Use e.g. Ctrl+Alt+Q.";
        if (RegisterHotKey(hwnd, id, m, v)) return "";
        return $"{label} shortcut {shortcut} could not be registered — another app may already use it. The app still works from the tray; pick a different key in Settings.";
    }

    public static bool Start(string captureShortcut, string eveningShortcut)
    {
        if (!OperatingSystem.IsWindows())
        {
            CaptureError = "Hotkeys are Windows-only.";
            return false;
        }
        if (_thread != null) return Restart(captureShortcut, eveningShortcut);
        _captureShortcut = captureShortcut;
        _eveningShortcut = eveningShortcut;
        _registered.Reset();
        _thread = new Thread(() =>
        {
            var wc = new WNDCLASS
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Proc),
                hInstance = GetModuleHandleW(null),
                lpszClassName = "JournalHotkeyWindow"
            };
            RegisterClassW(ref wc);
            _hwnd = CreateWindowExW(0, "JournalHotkeyWindow", "", 0, 0, 0, 0, 0,
                HWND_MESSAGE, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
            CaptureError = RegisterOne(_hwnd, 1, _captureShortcut, "Capture");
            EveningError = RegisterOne(_hwnd, 2, _eveningShortcut, "Evening review");
            _registered.Set();
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        })
        { IsBackground = true };
        _thread.Start();
        // Wait for registration to actually finish (was: a blind 200 ms sleep,
        // which could report success/failure before RegisterHotKey had run).
        _registered.Wait(TimeSpan.FromSeconds(2));
        return !AnyError;
    }

    /// <summary>
    /// Cleanly unregister the old shortcuts and register the new ones on
    /// the existing message window, so Settings changes apply without an
    /// app restart. Returns true only if both shortcuts registered.
    /// </summary>
    public static bool Restart(string captureShortcut, string eveningShortcut)
    {
        if (!OperatingSystem.IsWindows()) return false;
        if (_hwnd == IntPtr.Zero) return Start(captureShortcut, eveningShortcut);
        try { UnregisterHotKey(_hwnd, 1); } catch { }
        try { UnregisterHotKey(_hwnd, 2); } catch { }
        _captureShortcut = captureShortcut;
        _eveningShortcut = eveningShortcut;
        CaptureError = RegisterOne(_hwnd, 1, _captureShortcut, "Capture");
        EveningError = RegisterOne(_hwnd, 2, _eveningShortcut, "Evening review");
        return !AnyError;
    }

    public static void Stop()
    {
        try
        {
            if (_hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, 1);
                UnregisterHotKey(_hwnd, 2);
            }
        }
        catch { }
    }
}
