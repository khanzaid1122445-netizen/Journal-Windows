using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Journal;

public class DraftWindow : Window
{
    public DraftWindow(string draft, Action onSaveToNotion)
    {
        Title = "Evening draft — approve before saving";
        Width = 560; Height = 640;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        var stack = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Draft — nothing saves until you approve it",
            FontSize = 14, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
        });
        var box = Ui.TextBox(480, acceptsReturn: true, isReadOnly: true);
        box.Text = draft;
        stack.Children.Add(box);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var save = new Button
        {
            Content = "Save to Notion", Width = 200, Height = 44,
            Background = new SolidColorBrush(Color.Parse("#0F766E")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
        };
        save.Click += (_, _) => { onSaveToNotion(); Close(); };
        var close = new Button { Content = "Close, do not save", Width = 200, Height = 44 };
        close.Click += (_, _) => Close();
        row.Children.Add(save); row.Children.Add(close);
        stack.Children.Add(row);
        Content = stack;
    }
}
