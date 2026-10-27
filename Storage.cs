using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Journal;

public static class Storage
{
    public static string AppDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickCaptureJournaling");

    public static string CapturesPath => Path.Combine(AppDir, "captures.json");
    public static string EveningPath => Path.Combine(AppDir, "evening.json");
    public static string MorningPath => Path.Combine(AppDir, "morning.json");
    public static string SettingsPath => Path.Combine(AppDir, "settings.json");
    public static string AttachmentsDir => Path.Combine(AppDir, "attachments");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    static Storage()
    {
        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(AttachmentsDir);
    }

    public static List<Capture> LoadCaptures()
        => LoadList<Capture>(CapturesPath);

    public static void SaveCaptures(List<Capture> items)
        => SaveList(CapturesPath, items);

    public static void AddCapture(Capture c)
    {
        var list = LoadCaptures();
        list.Add(c);
        SaveCaptures(list);
    }

    public static List<EveningEntry> LoadEvening()
        => LoadList<EveningEntry>(EveningPath);

    public static void AddEvening(EveningEntry e)
    {
        var list = LoadEvening();
        list.Add(e);
        SaveList(EveningPath, list);
    }

    public static List<MorningIntention> LoadMorning()
        => LoadList<MorningIntention>(MorningPath);

    public static void AddMorning(MorningIntention m)
    {
        var list = LoadMorning();
        list.Add(m);
        SaveList(MorningPath, list);
    }

    public static AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch { /* corrupted settings: start fresh, never lose a capture over it */ }
        return new AppSettings();
    }

    public static void SaveSettings(AppSettings s)
        => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, JsonOpts));

    private static List<T> LoadList<T>(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? new List<T>();
        }
        catch { }
        return new List<T>();
    }

    private static void SaveList<T>(string path, List<T> items)
        => File.WriteAllText(path, JsonSerializer.Serialize(items, JsonOpts));

    public static string CopyAttachment(string sourcePath)
    {
        var name = Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(sourcePath);
        var dest = Path.Combine(AttachmentsDir, name);
        File.Copy(sourcePath, dest, overwrite: true);
        return name;
    }
}
