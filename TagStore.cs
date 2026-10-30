using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Journal;

/// <summary>
/// Custom-tag section mappings, stored locally only (never in code).
/// Base tags map to themselves. A custom tag maps to exactly one of the
/// six base sections, chosen by the user when the tag is created, or
/// later in Evening Review / Settings when a tag arrives from Notion
/// with no mapping yet. File: %AppData%\QuickCaptureJournaling\tag-mappings.json
/// </summary>
public static class TagStore
{
    public static readonly string[] BaseTags = TagsWindow.AllTags;

    private static string Path_ => Path.Combine(Storage.AppDir, "tag-mappings.json");

    private static Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);

    static TagStore() { Load(); }

    public static void Load()
    {
        try
        {
            if (File.Exists(Path_))
                _map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path_))
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch { _map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
        foreach (var t in BaseTags) _map[t] = t;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Storage.AppDir);
            // Base tags are implicit; only custom mappings are persisted.
            var custom = _map.Where(kv => !BaseTags.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            File.WriteAllText(Path_, JsonSerializer.Serialize(custom, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>Section tag for a tag, or null if a custom tag is unmapped.</summary>
    public static string? SectionFor(string tag)
    {
        if (BaseTags.Contains(tag, StringComparer.OrdinalIgnoreCase)) return tag;
        return _map.TryGetValue(tag, out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;
    }

    public static bool IsMapped(string tag) => SectionFor(tag) != null;

    public static void SetMapping(string tag, string sectionTag)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(sectionTag)) return;
        _map[tag.Trim()] = sectionTag.Trim();
        Save();
    }

    public static Dictionary<string, string> AllMappings()
        => new(_map, StringComparer.OrdinalIgnoreCase);
}
