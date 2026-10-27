using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Journal;

public class CaptureWindow : Window
{
    private readonly TextBox _input;
    private readonly TextBlock _saveStatus;
    private readonly TextBlock _notionStatus;
    private readonly TextBlock _hotkeyStatus;
    private readonly TextBlock _tagsText;
    private readonly HashSet<string> _tags = new();
    private readonly List<string> _attachments = new();

    public CaptureWindow()
    {
        Title = "Quick Capture";
        Width = 640; Height = 280;
        MinWidth = 640; MinHeight = 280;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        CanResize = false;
        Icon = App.LoadIcon();

        var root = new StackPanel { Margin = new Thickness(20, 12, 20, 12), Spacing = 10 };

        // Heading row: title left, hint right.
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var title = new TextBlock
        {
            Text = "Quick Capture", FontSize = 16, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
        };
        var hint = new TextBlock
        {
            Text = "Enter save · Shift+Enter newline · Esc dismiss",
            FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#6B7280")),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 0); Grid.SetColumn(hint, 1);
        head.Children.Add(title); head.Children.Add(hint);
        root.Children.Add(head);

        // Input row: text box + teal Add button.
        // BUG FIX (2026-10-27): the old TextBox set only Background and relied
        // on Avalonia's default theme, which this code-only app never loaded
        // (App.Initialize was empty), so on the user's PC it rendered as an
        // invisible paper-coloured area with no box, border or placeholder.
        // The box below is fully explicit and cannot collapse: white fill,
        // visible #D1D5DB border, dark ink text, padding, fixed height.
        // App.Initialize now also loads the Fluent theme, so the control
        // gets normal focus/border behaviour as well.
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _input = Ui.TextBox(56, watermark: "capture a thought…", acceptsReturn: true,
            fontSize: 15, cornerRadius: 12, padding: new Thickness(12, 10));
        _input.KeyDown += OnInputKeyDown;
        var add = new Button
        {
            Content = "➤ Add", Width = 108, Height = 56, Margin = new Thickness(12, 0, 0, 0),
            Background = new SolidColorBrush(Color.Parse("#0F766E")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")),
            FontWeight = FontWeight.SemiBold, CornerRadius = new CornerRadius(12)
        };
        add.Click += async (_, _) => await SaveAsync();
        Grid.SetColumn(_input, 0); Grid.SetColumn(add, 1);
        row.Children.Add(_input); row.Children.Add(add);
        root.Children.Add(row);

        // Bottom row: + button, tags label, Settings, save feedback.
        // Save feedback lives here only; Notion and hotkey status have
        // their own lines below, so one can never overwrite the other.
        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var plus = new Button { Content = "+", Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5) };
        plus.Click += (_, _) =>
        {
            var w = new TagsWindow(_tags, RefreshTags);
            w.ShowDialog(this);
            w.Closed += (_, _) =>
            {
                foreach (var f in w.AttachedFiles)
                    if (!_attachments.Contains(f)) _attachments.Add(f);
                RefreshTags();
            };
        };
        _tagsText = new TextBlock
        {
            Text = "Add file or tags", FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#6B7280")),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0)
        };
        var settingsBtn = new Button { Content = "Settings", Height = 32, Margin = new Thickness(12, 0, 0, 0), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5) };
        settingsBtn.Click += (_, _) => App.ShowSettings();
        _saveStatus = new TextBlock
        {
            Text = "", FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#0F766E")),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(plus, 0); Grid.SetColumn(_tagsText, 1);
        Grid.SetColumn(settingsBtn, 2); Grid.SetColumn(_saveStatus, 3);
        bottom.Children.Add(plus); bottom.Children.Add(_tagsText);
        bottom.Children.Add(settingsBtn); bottom.Children.Add(_saveStatus);
        root.Children.Add(bottom);

        // Separate status lines — neither can hide the other.
        _notionStatus = new TextBlock
        {
            Text = NotionStatusText(), FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#0F766E"))
        };
        _hotkeyStatus = new TextBlock
        {
            Text = HotkeyStatusText(), FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#6B7280"))
        };
        root.Children.Add(_notionStatus);
        root.Children.Add(_hotkeyStatus);

        Content = root;
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    public void ShowCapture()
    {
        RefreshStatuses();
        Show();
        Activate();
        _input.Focus();
    }

    private void RefreshStatuses()
    {
        _notionStatus.Text = NotionStatusText();
        _notionStatus.Foreground = NotionStatusText().StartsWith("Notion: connected")
            ? new SolidColorBrush(Color.Parse("#0F766E"))
            : new SolidColorBrush(Color.Parse("#92400E"));
        _hotkeyStatus.Text = HotkeyStatusText();
        _hotkeyStatus.Foreground = HotkeyService.AnyError
            ? new SolidColorBrush(Color.Parse("#92400E"))
            : new SolidColorBrush(Color.Parse("#6B7280"));
    }

    private void RefreshTags()
    {
        _tagsText.Text = _tags.Count == 0
            ? "Add file or tags"
            : "Tags: " + string.Join(", ", _tags) +
              (_attachments.Count > 0 ? $"  ·  {_attachments.Count} file(s)" : "");
    }

    /// <summary>
    /// Three states, never a bare "Notion connected" claim:
    /// (a) nothing entered yet, (b) entered but the last test failed,
    /// (c) last test succeeded. Never-tested-but-entered is its own state.
    /// </summary>
    private static string NotionStatusText()
    {
        var s = Storage.LoadSettings();
        if (string.IsNullOrWhiteSpace(s.EncryptedTokenBase64) || string.IsNullOrWhiteSpace(s.DatabaseId))
            return "Notion: not set up — open Settings to add your token and database ID. Captures still save on this PC.";
        if (s.LastNotionTest == "failed")
            return "Notion: test failed — " + (string.IsNullOrWhiteSpace(s.LastNotionError)
                ? "open Settings and test the connection."
                : s.LastNotionError);
        if (s.LastNotionTest == "ok")
            return "Notion: connected";
        return "Notion: token saved, not tested yet — open Settings and press Test Notion connection.";
    }

    private static string HotkeyStatusText()
    {
        var s = Storage.LoadSettings();
        var parts = new List<string>();
        if (HotkeyService.CaptureError != "") parts.Add("Capture (" + s.CaptureShortcut + "): " + HotkeyService.CaptureError);
        if (HotkeyService.EveningError != "") parts.Add("Evening review (" + s.EveningShortcut + "): " + HotkeyService.EveningError);
        if (parts.Count > 0)
            return "Hotkeys: " + string.Join(" ", parts) + " The app still works from the tray.";
        return $"Hotkeys: {s.CaptureShortcut} capture · {s.EveningShortcut} evening review (tray also works)";
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; return; }
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            _ = SaveAsync();
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            var tags = TagsWindow.AllTags;
            var idx = e.Key switch
            {
                Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, Key.D5 => 4, Key.D6 => 5,
                _ => -1
            };
            if (idx >= 0)
            {
                if (!_tags.Add(tags[idx])) _tags.Remove(tags[idx]);
                RefreshTags();
                e.Handled = true;
            }
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = SaveAsync();
        }
    }

    private async Task SaveAsync()
    {
        var text = (_input.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text) && _attachments.Count == 0)
        {
            _saveStatus.Text = "Nothing to save — type a thought or attach a file";
            return;
        }
        // Local first: a capture is never lost because Notion is down.
        var capture = new Capture
        {
            Text = text,
            Tags = _tags.ToList(),
            Attachments = _attachments.Select(Storage.CopyAttachment).ToList()
        };
        try { Storage.AddCapture(capture); }
        catch (Exception ex)
        {
            _saveStatus.Text = "Local save failed: " + ex.Message;
            return;
        }
        _input.Text = "";
        _tags.Clear();
        _attachments.Clear();
        RefreshTags();
        _saveStatus.Text = "Saved locally";

        var settings = Storage.LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.EncryptedTokenBase64) ||
            string.IsNullOrWhiteSpace(settings.DatabaseId))
        {
            _saveStatus.Text = "Saved locally — Notion not set up (open Settings)";
            RefreshStatuses();
            return;
        }
        try
        {
            var token = SecureToken.Unprotect(settings.EncryptedTokenBase64);
            if (token == null) { _saveStatus.Text = "Saved locally — Notion token unreadable"; return; }
            using var notion = new NotionClient(token);
            var name = string.IsNullOrWhiteSpace(text) ? "Capture" : text.Length > 60 ? text[..60] : text;
            var ok = await notion.CreatePageAsync(settings.DatabaseId, name,
                DateTime.UtcNow.ToString("yyyy-MM-dd"), "Capture", "Pending", "",
                capture.Tags, null);
            if (ok)
            {
                settings.LastNotionTest = "ok";
                settings.LastNotionError = "";
                Storage.SaveSettings(settings);
            }
            _saveStatus.Text = ok ? "Saved to Notion" : "Saved locally — Notion failed, will retry in evening review";
            RefreshStatuses();
        }
        catch
        {
            _saveStatus.Text = "Saved locally — Notion failed";
        }
    }
}
