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
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Journal;

public class App : Application
{
    /// <summary>Single version constant, shown in Settings. Must match
    /// &lt;Version&gt; in Journal.csproj (currently 1.0.4).</summary>
    public const string Version = "1.0.4";

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
        // BUG FIX (2026-10-27, theme consistency): the Fluent theme follows
        // Windows dark mode by default, while every window hard-codes a
        // light paper background (#FDF8F0). On a dark-mode PC that left
        // white theme text on light paper (invisible labels/buttons) and
        // a dark focused TextBox with dark ink text (black while typing).
        // Force light for the whole app, regardless of Windows settings.
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        // Global light fallbacks: no TextBlock / Button / CheckBox /
        // TextBox anywhere may rely on an inherited theme foreground.
        // Local values set on a control (e.g. teal buttons with white
        // text) still win over these style setters, by Avalonia precedence.
        var ink = new SolidColorBrush(Color.Parse("#1F2937"));
        var white = new SolidColorBrush(Color.Parse("#FFFFFF"));
        var border = new SolidColorBrush(Color.Parse("#D1D5DB"));
        var tealTint = new SolidColorBrush(Color.Parse("#CCFBF1"));
        Styles.Add(new Avalonia.Styling.Style(x => x.OfType<TextBlock>())
        {
            Setters = { new Avalonia.Styling.Setter(TextBlock.ForegroundProperty, ink) }
        });
        Styles.Add(new Avalonia.Styling.Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(Button.ForegroundProperty, ink),
                new Avalonia.Styling.Setter(Button.BackgroundProperty, white),
                new Avalonia.Styling.Setter(Button.BorderBrushProperty, border),
            }
        });
        Styles.Add(new Avalonia.Styling.Style(x => x.OfType<CheckBox>())
        {
            Setters = { new Avalonia.Styling.Setter(CheckBox.ForegroundProperty, ink) }
        });
        Styles.Add(new Avalonia.Styling.Style(x => x.OfType<TextBox>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(TextBox.BackgroundProperty, white),
                new Avalonia.Styling.Setter(TextBox.ForegroundProperty, ink),
                new Avalonia.Styling.Setter(TextBox.BorderBrushProperty, border),
                new Avalonia.Styling.Setter(TextBox.CaretBrushProperty, ink),
                new Avalonia.Styling.Setter(TextBox.SelectionBrushProperty, tealTint),
                new Avalonia.Styling.Setter(TextBox.SelectionForegroundBrushProperty, ink),
            }
        });
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

    public static async Task<string> CheckUpdatesAsync()
    {
        // Velopack first (installed app only, ask-first download and
        // install, stable only). A plain single-file exe is not
        // Velopack-installed (IsInstalled=false): fall through to
        // the old GitHub latest-release check, which only opens the
        // release page. Any other Velopack failure is also quiet:
        // an update check must never break startup, especially
        // while the feed is still private (see BUILD-NOTES.md).
        var velopackSettings = Storage.LoadSettings();
        if (VelopackUpdate.IsInstalled(velopackSettings))
        {
            try
            {
                var applied = await VelopackUpdate.CheckAndOfferAsync(velopackSettings);
                return applied ? "Update installed, restarting…" : "No update found.";
            }
            catch { /* quiet: update check never breaks startup */ return "Update check failed."; }
        }

        var (available, tag, url, notes) = await UpdateChecker.CheckAsync(Version);
        if (!available || string.IsNullOrWhiteSpace(url)) return "You are up to date.";
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
        return "Update available: " + tag;
    }
}
