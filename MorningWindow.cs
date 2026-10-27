using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Journal;

public class MorningWindow : Window
{
    private readonly TextBox _intention;
    private readonly TextBox _why;
    private readonly TextBlock _status;

    public MorningWindow()
    {
        Title = "Morning intention";
        Width = 480; Height = 400;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        var stack = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Morning intention", FontSize = 18, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Optional · One thought for the day ahead",
            FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#6B7280"))
        });
        _intention = Ui.TextBox(90, watermark: "Today, I intend to…", acceptsReturn: true);
        stack.Children.Add(_intention);
        stack.Children.Add(new TextBlock { Text = "Why this matters (optional)", FontSize = 12 });
        _why = Ui.TextBox(56, acceptsReturn: true);
        stack.Children.Add(_why);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var skip = new Button { Content = "Skip for today", Width = 160, Height = 44 };
        skip.Click += (_, _) => Hide();
        var save = new Button
        {
            Content = "Save intention", Width = 160, Height = 44,
            Background = new SolidColorBrush(Color.Parse("#0F766E")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
        };
        save.Click += async (_, _) => await SaveAsync();
        row.Children.Add(skip); row.Children.Add(save);
        stack.Children.Add(row);
        _status = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Color.Parse("#6B7280")) };
        stack.Children.Add(_status);
        Content = stack;
        Closing += (_, e) => { e.Cancel = true; Hide(); };
    }

    private async Task SaveAsync()
    {
        var text = (_intention.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text)) { _status.Text = "Type an intention first, or Skip."; return; }
        Storage.AddMorning(new MorningIntention { Intention = text, Why = _why.Text ?? "" });
        _status.Text = "Saved locally";
        var settings = Storage.LoadSettings();
        if (!string.IsNullOrWhiteSpace(settings.EncryptedTokenBase64) &&
            !string.IsNullOrWhiteSpace(settings.DatabaseId))
        {
            try
            {
                var token = SecureToken.Unprotect(settings.EncryptedTokenBase64);
                if (token != null)
                {
                    using var notion = new NotionClient(token);
                    var ok = await notion.CreatePageAsync(settings.DatabaseId,
                        "Morning Intention – " + DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        DateTime.UtcNow.ToString("yyyy-MM-dd"), "Morning Intention", "Saved",
                        _why.Text ?? "", new System.Collections.Generic.List<string>(),
                        new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
                        {
                            ["Intention"] = new() { text }
                        });
                    _status.Text = ok ? "Saved to Notion" : "Saved locally — Notion failed";
                }
            }
            catch { _status.Text = "Saved locally — Notion failed"; }
        }
        _intention.Text = ""; _why.Text = "";
    }
}
