using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Journal;

public class TagsWindow : Window
{
    public static readonly string[] AllTags =
        { "Event", "Grateful", "Improvement", "Sin", "Obstacle", "TIL" };

    private readonly List<CheckBox> _boxes = new();
    public HashSet<string> Selected { get; }
    public List<string> AttachedFiles { get; } = new();

    public TagsWindow(HashSet<string> selected, Action onChanged)
    {
        Selected = selected;
        Title = "Tags and files";
        Width = 360; Height = 340;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        var stack = new StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = "Tags (optional) — pick more than one",
            FontSize = 14, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937"))
        });
        foreach (var tag in AllTags)
        {
            var box = new CheckBox { Content = tag, IsChecked = selected.Contains(tag) };
            box.Checked += (_, _) => { Selected.Add(tag); onChanged(); };
            box.Unchecked += (_, _) => { Selected.Remove(tag); onChanged(); };
            _boxes.Add(box);
            stack.Children.Add(box);
        }
        var attach = new Button { Content = "Attach files…", Width = 160, Height = 40 };
        attach.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions { AllowMultiple = true });
            foreach (var f in files) AttachedFiles.Add(f.Path.LocalPath);
            if (AttachedFiles.Count > 0) Close();
        };
        stack.Children.Add(attach);
        var done = new Button
        {
            Content = "Done", Width = 160, Height = 40,
            Background = new SolidColorBrush(Color.Parse("#0F766E")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFF"))
        };
        done.Click += (_, _) => Close();
        stack.Children.Add(done);
        Content = stack;
    }
}
