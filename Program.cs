using System;
using System.IO.Pipes;
using System.Threading;
using Avalonia;

namespace Journal;

public static class Program
{
    // Held for the life of the process so the OS does not release it.
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack install/update hooks, as early as possible.
        // SetAutoApplyOnStartup(false) is deliberate: a downloaded
        // update must NOT be installed silently on the next launch.
        // Installation only happens after the explicit install prompt
        // in VelopackUpdate.CheckAndOfferAsync. For a plain single-file
        // exe this is a no-op; failure here must never stop the app.
        try { Velopack.VelopackApp.Build().SetAutoApplyOnStartup(false).Run(); }
        catch { /* not a Velopack install, or hooks unavailable: continue normally */ }

        // Single-instance guard: a second launched copy must NOT steal or
        // fail the first copy's global hotkeys (the reported "shortcut
        // could not be registered" when Journal.exe was already running).
        // Instead it signals the first copy via a named pipe and exits.
        // The guard is best-effort: if the mutex/pipe mechanism itself
        // fails on an unusual system, the app still starts normally.
        try
        {
            _singleInstanceMutex = new Mutex(true, "JournalQuickCaptureSingleInstance", out var createdNew);
            if (!createdNew)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", "JournalQuickCapturePipe", PipeDirection.Out);
                    client.Connect(1000);
                    var bytes = System.Text.Encoding.UTF8.GetBytes("capture");
                    client.Write(bytes, 0, bytes.Length);
                    client.Flush();
                }
                catch { /* first copy has no listener yet: just exit quietly */ }
                return;
            }
        }
        catch { /* mutex unavailable: fall through and start normally */ }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
