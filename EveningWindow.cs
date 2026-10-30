using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Journal;

public class EveningWindow : Window
{
    public static readonly (string Tag, string Heading)[] SectionMap =
    {
        ("Event", "1- EVENTS"),
        ("Grateful", "2- Grateful | Went Well"),
        ("Improvement", "3- Improvement Actions"),
        ("Sin", "4- Sins"),
        ("Obstacle", "5- Obstacles in ways of Goal Achievement"),
        ("TIL", "6- TIL"),
    };

    private readonly TextBlock _yesterdayText;
    private readonly TextBox _tomorrow;
    private readonly TextBlock _status;
    private bool? _yesterdayWorked;
    private Dictionary<string, List<string>> _sections = new();
    private List<Capture> _today = new();
    private EveningEntry? _yesterday;

    public EveningWindow()
    {
        Title = "Evening review";
        Width = 640; Height = 720;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        var scroll = new ScrollViewer();
        var stack = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Evening review", FontSize = 18, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
        });
        stack.Children.Add(new TextBlock
        {
            Text = "About 5 minutes · Review yesterday first",
            FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#6B7280"))
        });
        _yesterdayText = new TextBlock { FontSize = 13, TextWrapping = TextWrapping.Wrap };
        stack.Children.Add(_yesterdayText);
        var yn = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var yes = new Button { Content = "Yes, it worked", Width = 160, Height = 40 };
        yes.Click += (_, _) => { _yesterdayWorked = true; _yesterdayText.Text += "  → Yes"; };
        var no = new Button { Content = "No, it did not", Width = 160, Height = 40 };
        no.Click += (_, _) => { _yesterdayWorked = false; _yesterdayText.Text += "  → No"; };
        yn.Children.Add(yes); yn.Children.Add(no);
        stack.Children.Add(yn);
        _sectionsPanel = new StackPanel { Spacing = 8 };
        stack.Children.Add(_sectionsPanel);
        stack.Children.Add(new TextBlock
        {
            Text = "One change for tomorrow (exactly one)",
            FontSize = 13, FontWeight = FontWeight.SemiBold
        });
        _tomorrow = Ui.TextBox(56, acceptsReturn: true);
        stack.Children.Add(_tomorrow);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var draftBtn = new Button { Content = "Show full draft", Width = 200, Height = 44 };
        draftBtn.Click += (_, _) => new DraftWindow(BuildDraft(), () => _ = SaveAsync()).ShowDialog(this);
        var saveBtn = new Button
        {
            Content = "Save to Notion", Width = 200, Height = 44,
            Background = new SolidColorBrush(Color.Parse("#0F766E")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
        };
        saveBtn.Click += async (_, _) => await SaveAsync();
        row.Children.Add(draftBtn); row.Children.Add(saveBtn);
        stack.Children.Add(row);
        _status = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#0F766E")) };
        stack.Children.Add(_status);
        scroll.Content = stack;
        Content = scroll;
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    private readonly StackPanel _sectionsPanel;

    public void Refresh()
    {
        var all = Storage.LoadCaptures();
        var today = DateTime.UtcNow.Date;
        // Captures are stored in UTC; group by local day on the user's PC.
        _today = all.Where(c => c.CreatedUtc.ToLocalTime().Date == today && !c.Reviewed).ToList();
        _sections = new Dictionary<string, List<string>>();
        foreach (var (tag, heading) in SectionMap)
        {
            // Base tags go to their own section; a custom tag goes to its
            // mapped section, and a capture still appears under every
            // base tag it carries as well (no duplicates within a section).
            var lines = _today.Where(c => c.Tags.Any(t =>
                string.Equals(t, tag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(TagStore.SectionFor(t), tag, StringComparison.OrdinalIgnoreCase)))
                .Select(c => c.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            _sections[heading] = lines;
        }
        var evenings = Storage.LoadEvening();
        _yesterday = evenings.Where(e => e.DateUtc.ToLocalTime().Date < today)
            .OrderByDescending(e => e.DateUtc).FirstOrDefault();
        _yesterdayText.Text = _yesterday == null
            ? "Yesterday's change: none yet — this is your first evening review."
            : "Yesterday's change: " + _yesterday.TomorrowAction + "  —  Did it work today?";
        if (_yesterday != null && string.IsNullOrWhiteSpace(_tomorrow.Text))
            _tomorrow.Text = _yesterday.TomorrowAction; // carry forward only if unresolved; user edits it

        _sectionsPanel.Children.Clear();
        foreach (var (tag, heading) in SectionMap)
        {
            _sectionsPanel.Children.Add(new TextBlock
            {
                Text = heading, FontSize = 14, FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
            });
            var lines = _sections[heading];
            _sectionsPanel.Children.Add(new TextBlock
            {
                Text = lines.Count == 0 ? "Empty — stays empty" : string.Join("\n", lines.Select(l => "• " + l)),
                FontSize = 13, TextWrapping = TextWrapping.Wrap
            });
        }
        // Custom tags with no section mapping yet: prompt here, do not
        // lose the capture — it is listed here until a section is picked.
        var unmapped = _today.SelectMany(c => c.Tags).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t => !TagStore.IsMapped(t)).ToList();
        foreach (var tag in unmapped)
        {
            var capturedTag = tag;
            _sectionsPanel.Children.Add(new TextBlock { Text = $"Tag “{tag}” has no section yet — pick one:", FontSize = 13, FontWeight = FontWeight.SemiBold });
            var mapRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var combo = new ComboBox { Width = 180, Height = 36, ItemsSource = SectionMap.Select(s => s.Tag).ToList(), SelectedIndex = 0 };
            var mapBtn = new Button { Content = "Save mapping", Height = 36 };
            mapBtn.Click += (_, _) =>
            {
                TagStore.SetMapping(capturedTag, combo.SelectedItem as string ?? SectionMap[0].Tag);
                Refresh();
            };
            mapRow.Children.Add(combo); mapRow.Children.Add(mapBtn);
            _sectionsPanel.Children.Add(mapRow);
            var lines = _today.Where(c => c.Tags.Contains(capturedTag, StringComparer.OrdinalIgnoreCase)).Select(c => c.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            _sectionsPanel.Children.Add(new TextBlock { Text = string.Join("\n", lines.Select(l => "• " + l)), FontSize = 13, TextWrapping = TextWrapping.Wrap });
        }
        var untagged = _today.Where(c => c.Tags.Count == 0).Select(c => c.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (untagged.Count > 0)
        {
            _sectionsPanel.Children.Add(new TextBlock { Text = "Untagged (not in the entry unless you tag them)", FontSize = 13, FontWeight = FontWeight.SemiBold });
            _sectionsPanel.Children.Add(new TextBlock { Text = string.Join("\n", untagged.Select(l => "• " + l)), FontSize = 13, TextWrapping = TextWrapping.Wrap });
        }
    }

    private string BuildDraft()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Evening entry — " + DateTime.Now.ToString("yyyy-MM-dd"));
        if (_yesterday != null)
            sb.AppendLine($"Yesterday's change: {_yesterday.TomorrowAction} — worked: {(_yesterdayWorked == null ? "not answered" : _yesterdayWorked.Value ? "Yes" : "No")}");
        sb.AppendLine();
        foreach (var (tag, heading) in SectionMap)
        {
            sb.AppendLine(heading);
            foreach (var line in _sections[heading]) sb.AppendLine("• " + line);
            sb.AppendLine();
        }
        sb.AppendLine("One change for tomorrow: " + (_tomorrow.Text ?? ""));
        return sb.ToString();
    }

    private async Task SaveAsync()
    {
        var tomorrow = (_tomorrow.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(tomorrow))
        {
            _status.Text = "Type exactly one change for tomorrow first.";
            return;
        }
        var entry = new EveningEntry
        {
            YesterdayAction = _yesterday?.TomorrowAction,
            YesterdayWorked = _yesterdayWorked,
            Sections = _sections,
            TomorrowAction = tomorrow,
        };
        Storage.AddEvening(entry);
        var settings = Storage.LoadSettings();
        var notionOk = false;
        if (!string.IsNullOrWhiteSpace(settings.EncryptedTokenBase64) &&
            !string.IsNullOrWhiteSpace(settings.DatabaseId))
        {
            try
            {
                var token = SecureToken.Unprotect(settings.EncryptedTokenBase64);
                if (token != null)
                {
                    using var notion = new NotionClient(token);
                    notionOk = await notion.CreatePageAsync(settings.DatabaseId,
                        DateTime.Now.ToString("yyyy-MM-dd"),
                        DateTime.Now.ToString("yyyy-MM-dd"), "Evening Entry", "Saved",
                        tomorrow, _today.SelectMany(c => c.Tags).Distinct().ToList(), _sections);
                }
            }
            catch { notionOk = false; }
        }
        entry.SavedToNotion = notionOk;
        // Mark today's captures Reviewed (locally; their Notion pages stay).
        var all = Storage.LoadCaptures();
        foreach (var c in all.Where(c => _today.Any(t => t.Id == c.Id))) c.Reviewed = true;
        Storage.SaveCaptures(all);
        _status.Text = notionOk
            ? "Saved to Notion"
            : "Saved locally — Notion not configured or failed. Captures are marked Reviewed locally.";
    }
}
