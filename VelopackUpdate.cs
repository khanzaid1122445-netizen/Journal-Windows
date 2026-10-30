using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Velopack;
using Velopack.Sources;

namespace Journal;

/// <summary>
/// Velopack auto update, ask-first at every step, never silent.
///
/// Flow, for a Velopack-installed app only:
///   1. CheckForUpdatesAsync against the feed in settings.
///      Stable only: GithubSource is created with prerelease=false.
///   2. If DownloadAskFirst, ask "Download this update?" first.
///   3. DownloadUpdatesAsync.
///   4. If InstallAskFirst, ask "Install and restart?" first.
///   5. ApplyUpdatesAndRestart — this exits the app and Velopack
///      restarts it on the new version.
///
/// For a plain single-file exe (not Velopack-installed),
/// IsInstalled is false, so App.CheckUpdatesAsync falls back to
/// the old GitHub latest-release check that only opens the
/// release page. No
/// update is ever downloaded or installed silently, and
/// VelopackApp is configured with SetAutoApplyOnStartup(false)
/// in Program.cs, so a downloaded update is not applied on the
/// next launch without the install prompt above.
///
/// Feed selection: if the feed URL looks like a GitHub repo
/// (github.com/owner/repo, two path segments, no file name),
/// GithubSource is used, so vpk-packed releases attached to
/// GitHub Releases are found. Otherwise the URL is passed
/// straight to UpdateManager, which treats it as a plain HTTP
/// Velopack feed folder (a folder containing a RELEASES file).
/// The feed URL itself is public configuration, never a secret:
/// no token is ever passed to GithubSource here (token="").
/// </summary>
public static class VelopackUpdate
{
    public static bool IsGithubRepoUrl(string feed)
    {
        try
        {
            var uri = new Uri(feed.Trim());
            if (!uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
                return false;
            var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2;
        }
        catch { return false; }
    }

    /// <summary>True only for a real Velopack install. A plain
    /// single-file exe returns false here — Velopack 0.0.942 has no
    /// NotInstalledException type, so IsInstalled is the gate, not
    /// an exception type. Callers fall back to the release-page
    /// check when this is false.</summary>
    public static bool IsInstalled(AppSettings settings)
    {
        try { return CreateManager(settings).IsInstalled; }
        catch { return false; }
    }

    public static UpdateManager CreateManager(AppSettings settings)
    {
        var feed = string.IsNullOrWhiteSpace(settings.UpdateFeedUrl)
            ? "https://github.com/khanzaid1122445-netizen/Journal-Windows"
            : settings.UpdateFeedUrl.Trim();
        if (IsGithubRepoUrl(feed))
            return new UpdateManager(new GithubSource(feed, "", false));
        return new UpdateManager(feed);
    }

    /// <summary>Returns true if a Velopack update was applied (app
    /// is exiting/restarting), false if no update or declined.
    /// Callers must check IsInstalled first; this method assumes
    /// a Velopack install.</summary>
    public static async Task<bool> CheckAndOfferAsync(AppSettings settings)
    {
        var mgr = CreateManager(settings);
        // Caller checked IsInstalled first; a non-installed app
        // must not reach this path.
        var update = await mgr.CheckForUpdatesAsync();
        if (update == null || update.TargetFullRelease == null)
            return false;

        var version = update.TargetFullRelease.Version?.ToString() ?? "?";
        var notes = update.TargetFullRelease.NotesMarkdown ?? "";

        if (settings.UpdateDownloadAskFirst)
        {
            var download = await AskAsync(
                "Update available: " + version,
                string.IsNullOrWhiteSpace(notes) ? "A new version is available." : notes,
                "Download update", "Later");
            if (!download) return false;
        }

        await mgr.DownloadUpdatesAsync(update);

        if (settings.UpdateInstallAskFirst)
        {
            var install = await AskAsync(
                "Update downloaded: " + version,
                "Install now and restart Journal?",
                "Install and restart", "Later");
            if (!install) return false;
        }

        // This exits the app immediately; Velopack restarts it.
        mgr.ApplyUpdatesAndRestart(update.TargetFullRelease);
        return true;
    }

    /// <summary>Two-button confirm dialog on the UI thread.</summary>
    private static Task<bool> AskAsync(string title, string message, string confirm, string cancel)
    {
        var tcs = new TaskCompletionSource<bool>();
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var w = new Window
                {
                    Title = title, Width = 480, Height = 340,
                    Background = new SolidColorBrush(Color.Parse("#FDF8F0"))
                };
                var stack = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
                stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Ui.InkBrush, TextWrapping = TextWrapping.Wrap });
                stack.Children.Add(new TextBlock { Text = message, FontSize = 12, Foreground = Ui.InkBrush, TextWrapping = TextWrapping.Wrap });
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                var ok = new Button
                {
                    Content = confirm, Width = 200, Height = 44,
                    Background = new SolidColorBrush(Color.Parse("#0F766E")),
                    Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
                };
                ok.Click += (_, _) => { w.Close(); tcs.TrySetResult(true); };
                var later = new Button { Content = cancel, Width = 120, Height = 44 };
                later.Click += (_, _) => { w.Close(); tcs.TrySetResult(false); };
                row.Children.Add(ok); row.Children.Add(later);
                stack.Children.Add(row);
                w.Content = new ScrollViewer { Content = stack };
                w.Closed += (_, _) => tcs.TrySetResult(false);
                w.Show();
            }
            catch { tcs.TrySetResult(false); }
        });
        return tcs.Task;
    }
}
