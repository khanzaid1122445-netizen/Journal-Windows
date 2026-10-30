using System;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Journal;

/// <summary>
/// File-chip preview. Images open in-app, large. .wav voice notes get a
/// Play/Stop button (MCI). Anything else opens with the system default
/// app, and the window explains that. Clicking the chip's × never
/// reaches here — × only removes the file from the capture.
/// </summary>
public class PreviewWindow : Window
{
    public PreviewWindow(string filePath)
    {
        Title = "Preview — " + Path.GetFileName(filePath);
        Width = 640; Height = 520;
        Background = new SolidColorBrush(Color.Parse("#FDF8F0"));
        Icon = App.LoadIcon();
        var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = Path.GetFileName(filePath), FontSize = 14, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#1F2937")), TextWrapping = TextWrapping.Wrap
        });
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
        if (isImage)
        {
            try
            {
                var img = new Image { Source = new Bitmap(filePath), Stretch = Stretch.Uniform, MaxHeight = 420 };
                stack.Children.Add(img);
            }
            catch (Exception ex)
            {
                stack.Children.Add(new TextBlock { Text = "Could not show this image: " + ex.Message, TextWrapping = TextWrapping.Wrap });
                AddOpenButton(stack, filePath);
            }
        }
        else if (ext is ".wav" or ".mp3" or ".m4a" or ".ogg")
        {
            var status = new TextBlock { Text = "Voice note / audio file", FontSize = 13 };
            stack.Children.Add(status);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            var play = new Button { Content = "▶ Play", Width = 140, Height = 44, Background = new SolidColorBrush(Color.Parse("#0F766E")), Foreground = new SolidColorBrush(Color.Parse("#FFFFFF")) };
            play.Click += (_, _) =>
            {
                var err = ext == ".wav" ? VoiceRecorder.PlayWav(filePath) : null;
                if (err != null) status.Text = err;
                else if (ext != ".wav") { try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); } catch (Exception ex) { status.Text = ex.Message; } }
                else status.Text = "Playing…";
            };
            var stop = new Button { Content = "■ Stop", Width = 120, Height = 44 };
            stop.Click += (_, _) => { VoiceRecorder.StopPlayback(); status.Text = "Stopped."; };
            row.Children.Add(play); row.Children.Add(stop);
            stack.Children.Add(row);
            AddOpenButton(stack, filePath);
        }
        else
        {
            stack.Children.Add(new TextBlock { Text = "No in-app preview for this file type — opening it with its usual app is the preview.", TextWrapping = TextWrapping.Wrap, FontSize = 13 });
            AddOpenButton(stack, filePath);
            try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); } catch { }
        }
        Content = new ScrollViewer { Content = stack };
        Closing += (_, _) => VoiceRecorder.StopPlayback();
    }

    private static void AddOpenButton(StackPanel stack, string filePath)
    {
        var open = new Button { Content = "Open with default app", Width = 220, Height = 44 };
        open.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true }); } catch { }
        };
        stack.Children.Add(open);
    }
}
