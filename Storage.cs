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

    /// <summary>v1.0.2 clean display names: strips the old GUID prefix
    /// (32 hex chars + "_") that used to be shown in chips and in
    /// Notion, and maps internal pasted/voice GUID names to clean ones.
    /// The result is what the user sees and what Notion receives.</summary>
    public static string CleanDisplayName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        // Strip one or more leading GUID_ prefixes.
        while (name.Length > 33 && name[32] == '_' &&
               System.Text.RegularExpressions.Regex.IsMatch(name[..32], "^[0-9a-fA-F]{32}$"))
            name = name[33..];
        // pasted_<GUID>.bmp (old clipboard screenshots) -> pasted-image.bmp
        if (name.StartsWith("pasted_", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
        {
            var middle = name["pasted_".Length..^4];
            if (middle.Length == 32 && System.Text.RegularExpressions.Regex.IsMatch(middle, "^[0-9a-fA-F]{32}$"))
                return "pasted-image.bmp";
        }
        return name;
    }

    /// <summary>Returns a clean name that does not collide in the
    /// attachments folder, appending _1, _2 only when needed. The
    /// uniqueness suffix stays on the internal file only when the
    /// clean name is taken; it is never a GUID.</summary>
    public static string UniqueCleanName(string cleanName)
    {
        var stem = Path.GetFileNameWithoutExtension(cleanName);
        var ext = Path.GetExtension(cleanName);
        var candidate = cleanName;
        for (var i = 1; File.Exists(Path.Combine(AttachmentsDir, candidate)); i++)
            candidate = stem + "_" + i + ext;
        return candidate;
    }

    public static string CopyAttachment(string sourcePath)
    {
        // v1.0.2: no GUID prefix. The stored file, the chip label and
        // the Notion upload filename all use the clean name.
        var clean = CleanDisplayName(Path.GetFileName(sourcePath));
        var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
        if (string.Equals(sourceDir, Path.GetFullPath(AttachmentsDir), StringComparison.OrdinalIgnoreCase))
        {
            // Already in the attachments folder (voice note, pasted
            // image): rename in place to the clean name if needed,
            // otherwise keep it — never copy it onto itself.
            var current = Path.GetFileName(sourcePath);
            if (current == clean) return current;
            var target = UniqueCleanName(clean);
            File.Move(sourcePath, Path.Combine(AttachmentsDir, target));
            return target;
        }
        var name = UniqueCleanName(clean);
        var dest = Path.Combine(AttachmentsDir, name);
        File.Copy(sourcePath, dest, overwrite: true);
        return name;
    }
}
