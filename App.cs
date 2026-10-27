using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;

namespace Journal;

public class App : Application
{
    private static CaptureWindow? _capture;
    private static EveningWindow? _evening;
    private static MorningWindow? _morning;
    private static SettingsWindow? _settings;

    public static void ShowCapture()
    {
        _capture ??= new CaptureWindow();
        _capture.Icon = LoadIcon();
        _capture.ShowCapture();
    }

    public static void ShowEvening()
    {
        _evening ??= new EveningWindow();
        _evening.Icon = LoadIcon();
        _evening.Refresh();
        _evening.Show();
        _evening.Activate();
    }

    public static void ShowMorning()
    {
        _morning ??= new MorningWindow();
        _morning.Icon = LoadIcon();
        _morning.Show();
        _morning.Activate();
    }

    public static void ShowSettings()
    {
        _settings ??= new SettingsWindow();
        _settings.Icon = LoadIcon();
        _settings.Show();
        _settings.Activate();
    }

    private static NativeMenuItem MakeItem(string text, Action action)
    {
        var item = new NativeMenuItem(text);
        item.Click += (_, _) => action();
        return item;
    }

    public override void Initialize()
    {
        // BUG FIX (2026-10-27): this was empty, so no Avalonia theme was
        // ever loaded and code-only controls (notably the capture TextBox)
        // rendered unstyled/invisible on the user's PC. The Fluent theme
        // gives every control its normal border, focus and watermark look;
        // CaptureWindow also sets all of its own colours explicitly now.
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var icon = LoadIcon();
            var menu = new NativeMenu();
            menu.Add(MakeItem("New capture", ShowCapture));
            menu.Add(MakeItem("Evening review", ShowEvening));
            menu.Add(MakeItem("Morning intention", ShowMorning));
            menu.Add(MakeItem("Settings", ShowSettings));
            menu.Add(new NativeMenuItemSeparator());
            menu.Add(MakeItem("Quit", () => { HotkeyService.Stop(); desktop.Shutdown(); }));
            var tray = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "Journal — Quick Capture",
                Menu = menu
            };
            tray.Clicked += (_, _) => ShowCapture();
            TrayIcon.SetIcons(this, new TrayIcons { tray });

            var settings = Storage.LoadSettings();
            HotkeyService.OnCapture = ShowCapture;
            HotkeyService.OnEvening = ShowEvening;
            HotkeyService.Start(settings.CaptureShortcut, settings.EveningShortcut);
            // Single-instance listener: a second launched copy (see
            // Program.Main) sends "capture" down this pipe and exits,
            // instead of stealing/failing the first copy's hotkeys.
            StartSingleInstanceListener();

            ShowCapture();

            if (settings.CheckForUpdates)
                _ = CheckUpdatesAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// App icon: bold white J on teal #0F766E, rounded square. The .ico
    /// (16/32/48/64/128/256) is preferred for the Windows tray, where a
    /// 16x16 glyph must stay readable; PNG is the fallback.
    /// </summary>
    public static WindowIcon? LoadIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in new[] { "Journal.icon.ico", "Journal.icon.png" })
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream != null) return new WindowIcon(stream);
            }
        }
        catch { }
        return null;
    }

    private static void StartSingleInstanceListener()
    {
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        "JournalQuickCapturePipe", PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    server.WaitForConnection();
                    // Any message from a second copy just means "come forward".
                    Avalonia.Threading.Dispatcher.UIThread.Post(ShowCapture);
                    try { server.Disconnect(); } catch { }
                }
                catch { Thread.Sleep(500); }
            }
        })
        { IsBackground = true };
        thread.Start();
    }

    private static async Task CheckUpdatesAsync()
    {
        var (available, tag, url, notes) = await UpdateChecker.CheckAsync("1.0.0");
        if (!available || string.IsNullOrWhiteSpace(url)) return;
        var w = new Window
        {
            Title = "Update available", Width = 480, Height = 320,
            Background = new SolidColorBrush(Color.Parse("#FDF8F0"))
        };
        var stack = new Avalonia.Controls.StackPanel { Margin = new Thickness(20), Spacing = 10 };
        stack.Children.Add(new TextBlock { Text = "Update available: " + tag, FontSize = 16, FontWeight = FontWeight.SemiBold });
        stack.Children.Add(new TextBlock { Text = notes, TextWrapping = TextWrapping.Wrap, FontSize = 12 });
        var open = new Button
        {
            Content = "Open release page", Width = 200, Height = 44,
            Background = new SolidColorBrush(Color.Parse("#0F766E")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
        };
        open.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            w.Close();
        };
        var later = new Button { Content = "Later", Width = 120, Height = 44 };
        later.Click += (_, _) => w.Close();
        stack.Children.Add(open); stack.Children.Add(later);
        w.Content = stack;
        w.Show();
    }
}
