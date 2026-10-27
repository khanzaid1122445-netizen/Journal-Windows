using System;
using System.Collections.Generic;

namespace Journal;

public class Capture
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<string> Attachments { get; set; } = new();
    public bool Reviewed { get; set; }
    public string Type { get; set; } = "Capture";
}

public class MorningIntention
{
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public string Intention { get; set; } = "";
    public string Why { get; set; } = "";
}

public class EveningEntry
{
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public string? YesterdayAction { get; set; }
    public bool? YesterdayWorked { get; set; }
    public Dictionary<string, List<string>> Sections { get; set; } = new();
    public string TomorrowAction { get; set; } = "";
    public bool SavedToNotion { get; set; }
}

public class AppSettings
{
    // The Notion token is NEVER stored in plain text here.
    // Only its DPAPI-encrypted form (Windows) is stored, as base64.
    public string EncryptedTokenBase64 { get; set; } = "";
    public string DatabaseId { get; set; } = "";
    public string CaptureShortcut { get; set; } = "Ctrl+Alt+Q";
    public string EveningShortcut { get; set; } = "Ctrl+Alt+J";
    public bool CheckForUpdates { get; set; } = true;
    public bool HideToTray { get; set; } = true;
    // Last Notion test outcome: "" = never tested, "ok" = connected, "failed" = test failed.
    // The failure reason is shown to the user; it never contains a token.
    public string LastNotionTest { get; set; } = "";
    public string LastNotionError { get; set; } = "";
}
