using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journal;

public static class UpdateChecker
{
    // Note: this only works if the repo/releases are public. The user's
    // Journal-Windows repo is currently private, so an unauthenticated
    // check will fail and the app will simply continue normally.
    public static async Task<(bool Available, string Tag, string Url, string Notes)> CheckAsync(string currentVersion)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Journal-App");
            var json = await http.GetStringAsync(
                "https://api.github.com/repos/khanzaid1122445-netizen/Journal-Windows/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() ?? "" : "";
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var clean = tag.TrimStart('v');
            if (Version.TryParse(clean, out var latest) && Version.TryParse(currentVersion, out var current))
                return (latest > current, tag, url, notes);
        }
        catch { }
        return (false, "", "", "");
    }
}
