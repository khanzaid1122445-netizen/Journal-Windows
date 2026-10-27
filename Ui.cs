using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Journal;

/// <summary>
/// ONE shared text-field style for the whole app, applied to every TextBox.
/// Bug fix (2026-10-27): every window built its TextBoxes with only a white
/// Background (or nothing at all) and relied on Avalonia's default theme,
/// which this code-only app never loaded — so on the user's PC the fields
/// rendered as invisible gaps: label visible, no box, no border, no text.
/// A field built with this helper cannot be invisible: explicit white fill,
/// visible #D1D5DB border, 1.5px, dark ink text, padding, and a minimum
/// height that cannot collapse. Do not create a TextBox elsewhere without
/// going through this helper (or setting every one of these properties).
/// Light theme only: #FDF8F0 paper, #1F2937 ink, teal #0F766E accent.
/// </summary>
public static class Ui
{
    public static TextBox TextBox(
        double height = 40,
        string? watermark = null,
        bool acceptsReturn = false,
        bool isReadOnly = false,
        double fontSize = 14,
        double cornerRadius = 10,
        Thickness? padding = null)
    {
        var box = new TextBox
        {
            Height = height,
            MinHeight = height,
            AcceptsReturn = acceptsReturn,
            IsReadOnly = isReadOnly,
            FontSize = fontSize,
            Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D1D5DB")),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(cornerRadius),
            Padding = padding ?? new Thickness(10, 8),
            Foreground = new SolidColorBrush(Color.Parse("#1F2937")),
            CaretBrush = new SolidColorBrush(Color.Parse("#1F2937")),
            // Single-line fields centre their text; tall multi-line fields
            // must start at the top, not float in the middle.
            VerticalContentAlignment = acceptsReturn && height > 60
                ? Avalonia.Layout.VerticalAlignment.Top
                : Avalonia.Layout.VerticalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            TextWrapping = acceptsReturn ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        if (!string.IsNullOrEmpty(watermark)) box.Watermark = watermark;
        return box;
    }
}
