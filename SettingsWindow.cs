using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Journal;

public class SettingsWindow : Window
{
    private readonly TextBox _tokenBox;
    private readonly TextBox _dbBox;
    private readonly TextBox _captureBox;
    private readonly TextBox _eveningBox;
    private readonly CheckBox _updatesBox;
    private readonly CheckBox _downloadAskBox;
    private readonly CheckBox _installAskBox;
    private readonly TextBox _feedBox;
    private readonly TextBlock _status;

    public SettingsWindow()
    {
        Title = "Settings";
        Width = 520; Height = 600;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        var s = Storage.LoadSettings();
        var stack = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Settings", FontSize = 18, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
        });
        // Version line, clearly visible at the top of Settings.
        // Single source: App.Version, which matches <Version> in Journal.csproj.
        stack.Children.Add(new TextBlock
        {
            Text = "Version " + App.Version, FontSize = 14, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#0F766E"))
        });
        stack.Children.Add(new TextBlock { Text = "Notion — enter your own token and database ID. Nothing is hard-coded in the app.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.InkBrush });
        stack.Children.Add(new TextBlock { Text = "Notion token (stored with Windows DPAPI, never in plain text)", Foreground = Ui.InkBrush });
        _tokenBox = Ui.TextBox(40);
        _tokenBox.PasswordChar = '\u2022';
        stack.Children.Add(_tokenBox);
        stack.Children.Add(new TextBlock { Text = "Notion database ID", Foreground = Ui.InkBrush });
        _dbBox = Ui.TextBox(40);
        _dbBox.Text = s.DatabaseId;
        stack.Children.Add(_dbBox);
        stack.Children.Add(new TextBlock { Text = "Shortcuts — type a new combination, e.g. Ctrl+Alt+Q", Foreground = Ui.InkBrush });
        stack.Children.Add(new TextBlock { Text = "Open capture", Foreground = Ui.InkBrush });
        _captureBox = Ui.TextBox(40);
        _captureBox.Text = s.CaptureShortcut;
        stack.Children.Add(_captureBox);
        stack.Children.Add(new TextBlock { Text = "Open evening review", Foreground = Ui.InkBrush });
        _eveningBox = Ui.TextBox(40);
        _eveningBox.Text = s.EveningShortcut;
        stack.Children.Add(_eveningBox);
        _updatesBox = new CheckBox { Content = "Check for updates on startup (Velopack, stable only, ask first)", IsChecked = s.CheckForUpdates, Foreground = Ui.InkBrush };
        stack.Children.Add(_updatesBox);
        stack.Children.Add(new TextBlock { Text = "Update feed — GitHub repo URL, or a public Velopack feed folder. Auto update only works once this feed is public; a private repo feed is ignored quietly.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Ui.InkBrush });
        _feedBox = Ui.TextBox(40);
        _feedBox.Text = s.UpdateFeedUrl;
        stack.Children.Add(_feedBox);
        _downloadAskBox = new CheckBox { Content = "Ask before downloading an update", IsChecked = s.UpdateDownloadAskFirst, Foreground = Ui.InkBrush };
        stack.Children.Add(_downloadAskBox);
        _installAskBox = new CheckBox { Content = "Ask before installing an update", IsChecked = s.UpdateInstallAskFirst, Foreground = Ui.InkBrush };
        stack.Children.Add(_installAskBox);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        // Both buttons pin their own height and Top alignment so neither
        // stretches to fill the row (the reported "Save settings" sizing bug).
        var test = new Button { Content = "Test Notion connection", Width = 200, Height = 44, VerticalAlignment = VerticalAlignment.Top, Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Avalonia.Thickness(1.5) };
        test.Click += async (_, _) => await TestAsync();
        var save = new Button
        {
            Content = "Save settings", Width = 160, Height = 44, VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.Parse("#0F766E")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
        };
        save.Click += (_, _) => Save();
        row.Children.Add(test); row.Children.Add(save);
        stack.Children.Add(row);
        _status = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#374151")) };
        _status.Text = string.IsNullOrWhiteSpace(s.EncryptedTokenBase64)
            ? "Notion not configured yet. Captures still save locally."
            : "A Notion token is already stored (DPAPI). Leave the token box empty to keep it.";
        stack.Children.Add(_status);
        Content = new ScrollViewer { Content = stack };
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    private async Task TestAsync()
    {
        var (token, db) = CurrentTokenAndDb();
        if (token == null || string.IsNullOrWhiteSpace(db))
        {
            _status.Text = "Enter a token and database ID first.";
            return;
        }
        _status.Text = "Checking Notion…";
        using var notion = new NotionClient(token);
        var (ok, reason) = await notion.TestDatabaseWithReasonAsync(db);
        var s2 = Storage.LoadSettings();
        s2.LastNotionTest = ok ? "ok" : "failed";
        s2.LastNotionError = ok ? "" : reason;
        Storage.SaveSettings(s2);
        _status.Text = ok
            ? "Notion connected — the token and database ID work."
            : "Notion test failed — " + reason;
    }

    private (string? Token, string Db) CurrentTokenAndDb()
    {
        var s = Storage.LoadSettings();
        string? token = null;
        if (!string.IsNullOrWhiteSpace(_tokenBox.Text))
        {
            try { token = _tokenBox.Text; }
            catch { token = null; }
        }
        else if (!string.IsNullOrWhiteSpace(s.EncryptedTokenBase64))
        {
            try { token = SecureToken.Unprotect(s.EncryptedTokenBase64); } catch { token = null; }
        }
        var db = string.IsNullOrWhiteSpace(_dbBox.Text) ? s.DatabaseId : _dbBox.Text!.Trim();
        return (token, db);
    }

    private void Save()
    {
        var s = Storage.LoadSettings();
        var capture = (_captureBox.Text ?? "").Trim();
        var evening = (_eveningBox.Text ?? "").Trim();
        if (HotkeyService.IsBanned(capture) || HotkeyService.IsBanned(evening))
        {
            _status.Text = "That shortcut is reserved (Ctrl+Shift+Space, Ctrl+Shift+E, or Ctrl+Shift+Esc). Pick another one.";
            return;
        }
        if (!HotkeyService.TryParse(capture, out _, out _) || !HotkeyService.TryParse(evening, out _, out _))
        {
            _status.Text = "Shortcuts must look like Ctrl+Alt+Q (modifiers + one letter or digit).";
            return;
        }
        if (!string.IsNullOrWhiteSpace(_tokenBox.Text))
        {
            try { s.EncryptedTokenBase64 = SecureToken.Protect(_tokenBox.Text!); }
            catch (Exception ex) { _status.Text = "Could not protect the token: " + ex.Message; return; }
            _tokenBox.Text = "";
        }
        s.DatabaseId = (_dbBox.Text ?? "").Trim();
        s.CaptureShortcut = capture;
        s.EveningShortcut = evening;
        s.CheckForUpdates = _updatesBox.IsChecked == true;
        s.UpdateFeedUrl = (_feedBox.Text ?? "").Trim();
        s.UpdateDownloadAskFirst = _downloadAskBox.IsChecked == true;
        s.UpdateInstallAskFirst = _installAskBox.IsChecked == true;
        Storage.SaveSettings(s);
        // Re-register immediately on the existing hotkey window; no restart
        // is needed. A failed key is reported per shortcut, and the tray
        // keeps working either way.
        var hotkeysOk = HotkeyService.Restart(capture, evening);
        _status.Text = hotkeysOk
            ? "Settings saved. New shortcuts are active now."
            : "Settings saved, but a shortcut failed: "
              + (HotkeyService.CaptureError != "" ? HotkeyService.CaptureError + " " : "")
              + HotkeyService.EveningError
              + " Pick a different key, or use the tray menu.";
    }
}
