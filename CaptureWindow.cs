using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Journal;

/// <summary>
/// Minimal capture dialog, expanded 2026-10-27 (custom tags, icon
/// buttons, multi-file chips, drag-drop, clipboard paste, voice notes,
/// chip preview) — light theme only: paper #FDF8F0, ink #1F2937, teal
/// #0F766E, forced light. Layout, top to bottom: heading + hint, input
/// row with a teal arrow-only save button, tag chip row, custom-tag row,
/// file chip row, bottom row (+ for files, Record + timer, icon
/// buttons for Evening / Morning / Settings, brief save result).
/// NO permanent detail lines: no Notion status line and no hotkey
/// status/error line on this dialog. Hotkey failures are shown and
/// fixed in Settings only. No credential is hard-coded here; the token
/// lives only in DPAPI-protected settings on the user's PC.
/// </summary>
public class CaptureWindow : Window
{
    private readonly TextBox _input;
    private readonly TextBlock _saveStatus;
    private readonly TextBlock _recordStatus;
    private readonly WrapPanel _chipPanel;
    private readonly WrapPanel _filePanel;
    private readonly TextBox _newTagBox;
    private readonly ComboBox _newTagSection;
    private readonly Button _recordBtn;
    private readonly Dictionary<string, Button> _chipButtons = new();
    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _attachments = new();
    private List<string> _allTags = new(TagsWindow.AllTags);
    private DispatcherTimer? _recordTimer;
    private DateTime _recordStarted;

    public CaptureWindow()
    {
        Title = "Quick Capture";
        Width = 640; Height = 400;
        MinWidth = 640; MinHeight = 400;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        CanResize = false;
        Icon = App.LoadIcon();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        // v1.0.1 paste fix: a window-level KeyDown handler registered
        // with handledEventsToo, so Ctrl+V is seen even when the
        // focused TextBox already handled it for text paste. The
        // handler itself decides synchronously (see
        // OnWindowPasteKeyDown) whether the clipboard holds files /
        // images (attach + swallow the key) or text only (leave the
        // key alone so the TextBox pastes text normally).
        AddHandler(KeyDownEvent, OnWindowPasteKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);

        var root = new StackPanel { Margin = new Thickness(20, 12, 20, 12), Spacing = 8 };

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        head.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var title = new TextBlock { Text = "Quick Capture", FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(Color.Parse("#1F2937")) };
        var hint = new TextBlock { Text = "Enter save · Shift+Enter newline · Esc dismiss · Drag files here · Ctrl+V paste", FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#6B7280")), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(title, 0); Grid.SetColumn(hint, 1);
        head.Children.Add(title); head.Children.Add(hint);
        root.Children.Add(head);

        // Input row: text box + teal arrow-only save button (no "Add" word).
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _input = Ui.TextBox(56, watermark: "capture a thought…", acceptsReturn: true, fontSize: 15, cornerRadius: 12, padding: new Thickness(12, 10));
        _input.KeyDown += OnInputKeyDown;
        var save = new Button { Content = "➤", Width = 64, Height = 56, Margin = new Thickness(12, 0, 0, 0), Background = new SolidColorBrush(Color.Parse("#0F766E")), Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")), FontWeight = FontWeight.SemiBold, FontSize = 18, CornerRadius = new CornerRadius(12) };
        ToolTip.SetTip(save, "Save capture");
        save.Click += async (_, _) => await SaveAsync();
        Grid.SetColumn(_input, 0); Grid.SetColumn(save, 1);
        row.Children.Add(_input); row.Children.Add(save);
        root.Children.Add(row);

        // Tag chips, built from the fetched Notion options + base six.
        _chipPanel = new WrapPanel { Orientation = Orientation.Horizontal };
        root.Children.Add(_chipPanel);
        RebuildChips();

        // Custom tag row: name + section it maps to + Add tag.
        var tagRow = new Grid();
        tagRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        tagRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        tagRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        tagRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _newTagBox = Ui.TextBox(36, watermark: "New custom tag…", fontSize: 13, cornerRadius: 10);
        _newTagSection = new ComboBox { Width = 140, Height = 36, Margin = new Thickness(8, 0, 0, 0), ItemsSource = TagsWindow.AllTags.ToList(), SelectedIndex = 0 };
        ToolTip.SetTip(_newTagSection, "Section this custom tag maps to in Evening Review");
        var addTag = new Button { Content = "Add tag", Height = 36, Margin = new Thickness(8, 0, 0, 0), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5) };
        addTag.Click += async (_, _) => await AddCustomTagAsync();
        var refreshTags = new Button { Content = "↻ Tags", Height = 36, Margin = new Thickness(8, 0, 0, 0), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5) };
        ToolTip.SetTip(refreshTags, "Refresh tag list from Notion");
        refreshTags.Click += async (_, _) => await RefreshTagsFromNotionAsync();
        Grid.SetColumn(_newTagBox, 0); Grid.SetColumn(_newTagSection, 1); Grid.SetColumn(addTag, 2); Grid.SetColumn(refreshTags, 3);
        tagRow.Children.Add(_newTagBox); tagRow.Children.Add(_newTagSection); tagRow.Children.Add(addTag); tagRow.Children.Add(refreshTags);
        root.Children.Add(tagRow);

        // File chips: one chip per attachment, name (click = preview) + × (remove).
        _filePanel = new WrapPanel { Orientation = Orientation.Horizontal };
        root.Children.Add(_filePanel);
        RefreshFileChips();

        // Bottom row: + files, Paste, Record + timer, icon buttons, save result.
        var bottom = new Grid();
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var plus = new Button { Content = "+", Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5) };
        ToolTip.SetTip(plus, "Attach files (multiple)");
        plus.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { AllowMultiple = true });
            foreach (var f in files) AddAttachment(f.Path.LocalPath);
        };
        // v1.0.1: a visible Paste button as a guaranteed paste path,
        // in case Ctrl+V routing is still swallowed on some PC.
        var pasteBtn = new Button { Content = "📋 Paste", Height = 40, Margin = new Thickness(8, 0, 0, 0), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5) };
        ToolTip.SetTip(pasteBtn, "Paste files/images from clipboard (Ctrl+V)");
        pasteBtn.Click += (_, _) => PasteFilesFromClipboard();
        _recordBtn = new Button { Content = "🎙 Record", Height = 40, Margin = new Thickness(8, 0, 0, 0), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5) };
        ToolTip.SetTip(_recordBtn, "Record a voice note");
        _recordBtn.Click += (_, _) => ToggleRecording();
        _recordStatus = new TextBlock { Text = "", FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#B91C1C")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        var eveningBtn = IconButton("🌙", "Evening review", App.ShowEvening);
        var morningBtn = IconButton("🌅", "Morning intention", App.ShowMorning);
        var settingsBtn = IconButton("⚙", "Settings", App.ShowSettings);
        _saveStatus = new TextBlock { Text = "", FontSize = 11, Foreground = new SolidColorBrush(Color.Parse("#0F766E")), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(plus, 0); Grid.SetColumn(pasteBtn, 1); Grid.SetColumn(_recordBtn, 2); Grid.SetColumn(_recordStatus, 3);
        Grid.SetColumn(eveningBtn, 5); Grid.SetColumn(morningBtn, 6); Grid.SetColumn(settingsBtn, 7); Grid.SetColumn(_saveStatus, 8);
        bottom.Children.Add(plus); bottom.Children.Add(pasteBtn); bottom.Children.Add(_recordBtn); bottom.Children.Add(_recordStatus);
        bottom.Children.Add(eveningBtn); bottom.Children.Add(morningBtn); bottom.Children.Add(settingsBtn); bottom.Children.Add(_saveStatus);
        root.Children.Add(bottom);

        Content = root;
        Closing += (_, e) => { e.Cancel = true; VoiceRecorder.Cancel(); StopRecordTimer(); Hide(); };
    }

    private static Button IconButton(string glyph, string tip, Action action)
    {
        var b = new Button { Content = glyph, Width = 40, Height = 40, Margin = new Thickness(8, 0, 0, 0), CornerRadius = new CornerRadius(10), Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5), FontSize = 16 };
        ToolTip.SetTip(b, tip);
        b.Click += (_, _) => action();
        return b;
    }

    public void ShowCapture()
    {
        _saveStatus.Text = "";
        RefreshChips();
        RefreshFileChips();
        _ = RefreshTagsFromNotionAsync();
        Show();
        Activate();
        _input.Focus();
    }

    private void RebuildChips()
    {
        _chipPanel.Children.Clear();
        _chipButtons.Clear();
        foreach (var tag in _allTags)
        {
            var btn = new Button { Content = tag, Height = 30, Padding = new Thickness(12, 4), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(15), FontSize = 12 };
            var captured = tag;
            btn.Click += (_, _) => { if (!_tags.Add(captured)) _tags.Remove(captured); RefreshChips(); };
            _chipButtons[tag] = btn;
            _chipPanel.Children.Add(btn);
        }
        RefreshChips();
    }

    private void RefreshChips()
    {
        foreach (var (tag, btn) in _chipButtons)
        {
            var selected = _tags.Contains(tag);
            btn.Background = selected ? new SolidColorBrush(Color.Parse("#0F766E")) : new SolidColorBrush(Color.Parse("#FFFFFF"));
            btn.Foreground = selected ? new SolidColorBrush(Color.Parse("#FFFFFF")) : new SolidColorBrush(Color.Parse("#1F2937"));
            btn.BorderBrush = selected ? new SolidColorBrush(Color.Parse("#0F766E")) : new SolidColorBrush(Color.Parse("#D1D5DB"));
            btn.BorderThickness = new Thickness(1.5);
        }
    }

    private async Task RefreshTagsFromNotionAsync()
    {
        var settings = Storage.LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.EncryptedTokenBase64) || string.IsNullOrWhiteSpace(settings.DatabaseId)) return;
        try
        {
            var token = SecureToken.Unprotect(settings.EncryptedTokenBase64);
            if (token == null) return;
            using var notion = new NotionClient(token);
            var fetched = await notion.GetDatabaseTagOptionsAsync(settings.DatabaseId);
            if (fetched.Count == 0) return;
            var merged = new List<string>(TagsWindow.AllTags);
            foreach (var t in fetched) if (!merged.Contains(t, StringComparer.OrdinalIgnoreCase)) merged.Add(t);
            _allTags = merged;
            RebuildChips();
        }
        catch { /* offline / Notion down: keep the last known tag list */ }
    }

    private async Task AddCustomTagAsync()
    {
        var name = (_newTagBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) { _saveStatus.Text = "Type a custom tag name first."; return; }
        var section = _newTagSection.SelectedItem as string ?? TagsWindow.AllTags[0];
        TagStore.SetMapping(name, section);
        if (!_allTags.Contains(name, StringComparer.OrdinalIgnoreCase)) { _allTags.Add(name); RebuildChips(); }
        _tags.Add(name);
        RefreshChips();
        _newTagBox.Text = "";
        _saveStatus.Text = $"Tag “{name}” added, maps to {section}.";
        // Push the new option to Notion as well (PATCH). If that fails,
        // page creation with the tag still creates the option in Notion.
        var settings = Storage.LoadSettings();
        if (!string.IsNullOrWhiteSpace(settings.EncryptedTokenBase64) && !string.IsNullOrWhiteSpace(settings.DatabaseId))
        {
            try
            {
                var token = SecureToken.Unprotect(settings.EncryptedTokenBase64);
                if (token != null) { using var notion = new NotionClient(token); await notion.PatchDatabaseAddTagAsync(settings.DatabaseId, name); }
            }
            catch { }
        }
    }

    private void AddAttachment(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        if (!_attachments.Contains(path)) _attachments.Add(path);
        RefreshFileChips();
    }

    private void RefreshFileChips()
    {
        _filePanel.Children.Clear();
        foreach (var path in _attachments.ToList())
        {
            var captured = path;
            var displayName = Storage.CleanDisplayName(Path.GetFileName(path));
            var chip = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 8, 8) };
            // v1.0.2 thumbnail chips: image attachments show a small
            // thumbnail on the left of the chip (about 40px), then the
            // clean file name, then the red ×. Clicking the thumbnail
            // or the name still opens the full preview; × only removes.
            // Non-image files keep a 📎 icon + name chip, no thumbnail.
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";
            var body = new Button { Height = 40, Padding = new Thickness(6, 2), CornerRadius = new CornerRadius(15, 0, 0, 15), FontSize = 12, Background = Ui.WhiteBrush, Foreground = Ui.InkBrush, BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(1.5, 1.5, 0, 1.5) };
            var bodyContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Spacing = 6 };
            if (isImage)
            {
                try
                {
                    // v1.0.3: the thumbnail must not swallow the click.
                    // IsHitTestVisible=false lets the click reach the
                    // chip button, which opens the full preview, so
                    // clicking the thumbnail OR the name previews,
                    // while only the separate red × removes the file.
                    // Old .bmp attachments still try to load here and
                    // fall back to the icon if they cannot decode;
                    // new pasted screenshots are PNG (ClipboardFiles),
                    // which decodes reliably for thumbnails.
                    var thumb = new Avalonia.Controls.Image { Source = new Avalonia.Media.Imaging.Bitmap(path), Width = 36, Height = 36, Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
                    bodyContent.Children.Add(thumb);
                }
                catch { bodyContent.Children.Add(new TextBlock { Text = "🖼", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false }); }
            }
            else bodyContent.Children.Add(new TextBlock { Text = "📎", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
            bodyContent.Children.Add(new TextBlock { Text = displayName, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
            body.Content = bodyContent;
            ToolTip.SetTip(body, "Click to preview: " + displayName);
            body.Click += (_, _) => new PreviewWindow(captured).Show();
            var xBtn = new Button { Content = "×", Height = 40, Width = 30, Padding = new Thickness(0), CornerRadius = new CornerRadius(0, 15, 15, 0), FontSize = 14, Background = Ui.WhiteBrush, Foreground = new SolidColorBrush(Color.Parse("#DC2626")), BorderBrush = Ui.BorderBrush, BorderThickness = new Thickness(0, 1.5, 1.5, 1.5) };
            ToolTip.SetTip(xBtn, "Remove this file from this capture");
            xBtn.Click += (_, _) => { _attachments.Remove(captured); RefreshFileChips(); };
            chip.Children.Add(body); chip.Children.Add(xBtn);
            _filePanel.Children.Add(chip);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var files = e.Data.GetFiles();
            if (files != null) foreach (var f in files) AddAttachment(f.Path.LocalPath);
        }
        catch { }
    }

    private void ToggleRecording()
    {
        if (VoiceRecorder.IsRecording)
        {
            StopRecordTimer();
            Directory.CreateDirectory(Storage.AttachmentsDir);
            // v1.0.2: date-time voice name, no GUIDs, e.g.
            // voice_2026-10-27_23-14.wav (counter on a same-second clash).
            var wav = Path.Combine(Storage.AttachmentsDir,
                Storage.UniqueCleanName("voice_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".wav"));
            var err = VoiceRecorder.Stop(wav);
            _recordBtn.Content = "🎙 Record";
            if (err == null && File.Exists(wav)) { AddAttachment(wav); _recordStatus.Text = ""; _saveStatus.Text = "Voice note attached."; }
            else _recordStatus.Text = err ?? "Recording could not be saved.";
            return;
        }
        var startErr = VoiceRecorder.Start();
        if (startErr != null) { _recordStatus.Text = startErr; return; }
        _recordStarted = DateTime.Now;
        _recordBtn.Content = "■ Stop";
        _recordStatus.Text = "● 00:00";
        _recordTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _recordStarted;
            _recordStatus.Text = "● " + elapsed.ToString(@"mm\:ss");
        };
        _recordTimer.Start();
    }

    private void StopRecordTimer()
    {
        if (_recordTimer != null) { _recordTimer.Stop(); _recordTimer = null; }
    }

    /// <summary>v1.0.1 paste, synchronous on the UI thread.
    /// Returns true when files/images were pasted (caller swallows
    /// the key); false when the clipboard holds text only, so the
    /// TextBox pastes text normally.</summary>
    private bool PasteFilesFromClipboard()
    {
        // Check first, synchronously: this is what the old deferred
        // code could not do reliably, because the TextBox had already
        // consumed Ctrl+V by the time it ran.
        if (!ClipboardFiles.HasFileOrImage()) return false;
        var files = ClipboardFiles.TryGetPastedFiles();
        if (files.Count == 0)
        {
            _saveStatus.Text = "Clipboard paste failed — copy the file or image again.";
            return true;
        }
        foreach (var f in files) AddAttachment(f);
        _saveStatus.Text = "Pasted file(s) attached.";
        return true;
    }

    private void OnWindowPasteKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key != Key.V) return;
        // Only swallow the key when files/images were actually pasted.
        // Text-only clipboard: leave Handled false, TextBox pastes text.
        if (PasteFilesFromClipboard()) e.Handled = true;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (VoiceRecorder.IsRecording) ToggleRecording(); Hide(); e.Handled = true; return; }
        // Ctrl+V is handled at window level (OnWindowPasteKeyDown,
        // registered with handledEventsToo) — do not intercept it
        // here, or text paste and file paste would fight over it.
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            _ = SaveAsync();
            return;
        }
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            var tags = TagsWindow.AllTags;
            var idx = e.Key switch { Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, Key.D5 => 4, Key.D6 => 5, _ => -1 };
            if (idx >= 0) { if (!_tags.Add(tags[idx])) _tags.Remove(tags[idx]); RefreshChips(); e.Handled = true; }
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
        if (VoiceRecorder.IsRecording) ToggleRecording();
        var capture = new Capture { Text = text, Tags = _tags.ToList(), Attachments = _attachments.Select(Storage.CopyAttachment).ToList() };
        try { Storage.AddCapture(capture); }
        catch (Exception ex) { _saveStatus.Text = "Local save failed: " + ex.Message; return; }
        _input.Text = "";
        _tags.Clear();
        _attachments.Clear();
        RefreshChips();
        RefreshFileChips();
        _saveStatus.Text = "Saved locally";

        var settings = Storage.LoadSettings();
        if (string.IsNullOrWhiteSpace(settings.EncryptedTokenBase64) || string.IsNullOrWhiteSpace(settings.DatabaseId)) return;
        try
        {
            var token = SecureToken.Unprotect(settings.EncryptedTokenBase64);
            if (token == null) return;
            using var notion = new NotionClient(token);
            var name = string.IsNullOrWhiteSpace(text) ? "Capture" : text.Length > 60 ? text[..60] : text;
            // v1.0.1: real binary upload. Ordered page content:
            // images, other files (incl. .wav voice notes), then text.
            // capture.Attachments are bare filenames in the local
            // attachments folder (see Storage.CopyAttachment); the
            // upload code resolves them to full paths.
            var (pageOk, allFilesOk, _, _) = await notion.CreateCapturePageWithUploadsAsync(
                settings.DatabaseId, name,
                DateTime.UtcNow.ToString("yyyy-MM-dd"), "Capture", "Pending", "",
                capture.Tags, capture.Attachments, text);
            if (pageOk) { settings.LastNotionTest = "ok"; settings.LastNotionError = ""; Storage.SaveSettings(settings); }
            _saveStatus.Text = !pageOk ? "Saved locally"
                : !allFilesOk ? "Saved to Notion, file upload failed — files kept locally"
                : "Saved to Notion";
        }
        catch { _saveStatus.Text = "Saved locally"; }
    }
}
