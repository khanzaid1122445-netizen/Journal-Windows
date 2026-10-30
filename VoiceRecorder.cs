using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Journal;

/// <summary>
/// Voice recording via Win32 MCI (winmm.dll) — no NuGet package needed,
/// works on Windows with .NET 8. Produces a normal .wav file.
/// Start() / Stop(path) / Cancel(). Any MCI error is returned as text,
/// never thrown, so a missing microphone cannot crash the dialog.
/// </summary>
public static class VoiceRecorder
{
    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int mciSendString(string command, StringBuilder? returnString, int returnLength, IntPtr callback);

    private const string Alias = "journalrec";
    public static bool IsRecording { get; private set; }

    public static string? Start()
    {
        try
        {
            mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
            var err = mciSendString($"open new type waveaudio alias {Alias}", null, 0, IntPtr.Zero);
            if (err != 0) return $"Could not open the microphone (MCI error {err}). Check the microphone is allowed.";
            err = mciSendString($"record {Alias}", null, 0, IntPtr.Zero);
            if (err != 0) { mciSendString($"close {Alias}", null, 0, IntPtr.Zero); return $"Could not start recording (MCI error {err})."; }
            IsRecording = true;
            return null;
        }
        catch (Exception ex) { return "Recording failed: " + ex.Message; }
    }

    public static string? Stop(string wavPath)
    {
        if (!IsRecording) return "Not recording.";
        try
        {
            var err = mciSendString($"save {Alias} \"{wavPath}\"", null, 0, IntPtr.Zero);
            mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
            IsRecording = false;
            return err == 0 ? null : $"Could not save the recording (MCI error {err}).";
        }
        catch (Exception ex) { IsRecording = false; return "Recording failed: " + ex.Message; }
    }

    public static void Cancel()
    {
        try { mciSendString($"close {Alias}", null, 0, IntPtr.Zero); } catch { }
        IsRecording = false;
    }

    /// <summary>Play a .wav file via MCI (preview). Returns an error string or null.</summary>
    public static string? PlayWav(string wavPath)
    {
        try
        {
            mciSendString("close journalplay", null, 0, IntPtr.Zero);
            var err = mciSendString($"open \"{wavPath}\" type waveaudio alias journalplay", null, 0, IntPtr.Zero);
            if (err != 0) return $"Could not play this file (MCI error {err}).";
            err = mciSendString("play journalplay", null, 0, IntPtr.Zero);
            return err == 0 ? null : $"Could not play this file (MCI error {err}).";
        }
        catch (Exception ex) { return "Playback failed: " + ex.Message; }
    }

    public static void StopPlayback()
    {
        try { mciSendString("close journalplay", null, 0, IntPtr.Zero); } catch { }
    }
}
