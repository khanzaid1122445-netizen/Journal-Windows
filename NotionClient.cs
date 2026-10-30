using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journal;

/// <summary>
/// Minimal Notion client. No token or database ID is hard-coded anywhere:
/// both come from Settings, entered by the user.
/// </summary>
public class NotionClient : IDisposable
{
    private readonly HttpClient _http;

    public NotionClient(string token)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://api.notion.com/v1/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");
    }

    public async Task<bool> TestDatabaseAsync(string databaseId)
        => (await TestDatabaseWithReasonAsync(databaseId)).Ok;

    /// <summary>
    /// Tests the database and returns a user-readable reason on failure.
    /// The reason never contains the token.
    /// </summary>
    public async Task<(bool Ok, string Reason)> TestDatabaseWithReasonAsync(string databaseId)
    {
        try
        {
            var res = await _http.GetAsync($"databases/{databaseId}");
            if (res.IsSuccessStatusCode) return (true, "");
            return res.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized =>
                    (false, "401 Unauthorized — the token is invalid, or the database has not been shared with the integration."),
                System.Net.HttpStatusCode.Forbidden =>
                    (false, "403 Forbidden — the token lacks access to this database. Share the database with the integration."),
                System.Net.HttpStatusCode.NotFound =>
                    (false, "404 Not Found — the database ID looks wrong, or the database was not shared with the integration."),
                _ => (false, $"Notion returned {(int)res.StatusCode} {res.ReasonPhrase}. Check the token and database ID."),
            };
        }
        catch (HttpRequestException ex)
        {
            return (false, "Network error — check the internet connection. " + ex.Message);
        }
        catch (TaskCanceledException)
        {
            return (false, "Timed out — check the internet connection and try again.");
        }
        catch (Exception ex)
        {
            return (false, "Test failed: " + ex.Message);
        }
    }

    /// <summary>
    /// Reads the Tags multi-select options from the database itself, so a
    /// tag added directly in Notion appears in the app on next refresh.
    /// Returns an empty list (not an error) when Notion is unreachable.
    /// </summary>
    public async Task<List<string>> GetDatabaseTagOptionsAsync(string databaseId)
    {
        try
        {
            var res = await _http.GetAsync($"databases/{databaseId}");
            if (!res.IsSuccessStatusCode) return new List<string>();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            if (!doc.RootElement.TryGetProperty("properties", out var props)) return new List<string>();
            if (!props.TryGetProperty("Tags", out var tagsProp)) return new List<string>();
            if (!tagsProp.TryGetProperty("multi_select", out var ms)) return new List<string>();
            if (!ms.TryGetProperty("options", out var options)) return new List<string>();
            var outList = new List<string>();
            foreach (var o in options.EnumerateArray())
                if (o.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString()))
                    outList.Add(n.GetString()!);
            return outList;
        }
        catch { return new List<string>(); }
    }

    /// <summary>
    /// Adds a tag option to the database (PATCH). Returns false when the
    /// API rejects it; the caller still sends the tag on page creation,
    /// which Notion also uses to create a missing multi-select option.
    /// </summary>
    public async Task<bool> PatchDatabaseAddTagAsync(string databaseId, string tag)
    {
        try
        {
            var existing = await GetDatabaseTagOptionsAsync(databaseId);
            if (!existing.Contains(tag)) existing.Add(tag);
            var payload = new
            {
                properties = new
                {
                    Tags = new
                    {
                        multi_select = new
                        {
                            options = existing.ConvertAll(t => new { name = t })
                        }
                    }
                }
            };
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"databases/{databaseId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            var res = await _http.SendAsync(req);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ---- True binary file upload (v1.0.1) ----
    // Notion /v1/file_uploads lifecycle, per Notion's own docs:
    //   POST file_uploads {mode, filename, content_type} -> id (+upload_url)
    //   POST file_uploads/{id}/send (or upload_url) multipart "file"
    //     (+ "part_number" for multi_part) -> status "uploaded"
    //   POST file_uploads/{id}/complete (multi_part only)
    //   GET  file_uploads/{id} -> poll until status "uploaded"/"failed"
    //   Attach: image block {type:"image", image:{type:"file_upload",
    //     file_upload:{id}}} or file block {type:"file", file:{...}}.
    // Files <= 20 MB use single_part; larger files are split into
    // 10 MB parts and sent as multi_part, then completed.

    private static bool IsImagePath(string p)
    {
        var ext = Path.GetExtension(p).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    private static string ResolveAttachmentPath(string p)
        => Path.IsPathRooted(p) ? p : Path.Combine(Storage.AttachmentsDir, p);

    private static string ContentTypeFor(string p)
    {
        return Path.GetExtension(p).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".wav" => "audio/wav",
            ".mp3" => "audio/mpeg",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream",
        };
    }

    /// <summary>Uploads one local file to Notion. Returns the file_upload
    /// id on success, null on any failure (caller falls back to a
    /// filename paragraph and reports the failure, never losing data).</summary>
    public async Task<string?> UploadFileAsync(string path)
    {
        try
        {
            var full = ResolveAttachmentPath(path);
            if (!File.Exists(full)) return null;
            var info = new FileInfo(full);
            // v1.0.2: upload under the clean display name, never a
            // GUID prefix, even for attachments saved by older builds.
            var filename = Storage.CleanDisplayName(Path.GetFileName(full));
            var contentType = ContentTypeFor(full);
            const long singleLimit = 20L * 1024 * 1024;
            if (info.Length <= singleLimit)
            {
                var (id, uploadUrl) = await CreateFileUploadAsync("single_part", filename, contentType, null);
                if (id == null) return null;
                if (!await SendFilePartAsync(id, uploadUrl, full, null)) return null;
                return await PollUploadAsync(id, 5) ? id : null;
            }
            const long partSize = 10L * 1024 * 1024;
            var parts = (int)((info.Length + partSize - 1) / partSize);
            var (multiId, multiUrl) = await CreateFileUploadAsync("multi_part", filename, contentType, parts);
            if (multiId == null) return null;
            using var fs = new FileStream(full, FileMode.Open, FileAccess.Read);
            var buffer = new byte[partSize];
            for (var i = 1; i <= parts; i++)
            {
                var read = await fs.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) return null;
                var chunk = new byte[read];
                Array.Copy(buffer, chunk, read);
                if (!await SendFileBytesAsync(multiId, multiUrl, filename, contentType, chunk, i)) return null;
            }
            var complete = await _http.PostAsync($"file_uploads/{multiId}/complete",
                new StringContent("{}", Encoding.UTF8, "application/json"));
            if (!complete.IsSuccessStatusCode) return null;
            return await PollUploadAsync(multiId, 10) ? multiId : null;
        }
        catch { return null; }
    }

    private async Task<(string? Id, string? UploadUrl)> CreateFileUploadAsync(
        string mode, string filename, string contentType, int? numberOfParts)
    {
        // First try with filename + content_type (needed for multi_part
        // extension detection); if Notion rejects that shape for
        // single_part, fall back to mode-only.
        var attempts = new List<object>();
        if (numberOfParts.HasValue)
            attempts.Add(new { mode, filename, content_type = contentType, number_of_parts = numberOfParts.Value });
        else
        {
            attempts.Add(new { mode, filename, content_type = contentType });
            attempts.Add(new { mode });
        }
        foreach (var payload in attempts)
        {
            try
            {
                var res = await _http.PostAsync("file_uploads",
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
                if (!res.IsSuccessStatusCode) continue;
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                string? id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                string? url = root.TryGetProperty("upload_url", out var urlEl) ? urlEl.GetString() : null;
                if (!string.IsNullOrWhiteSpace(id)) return (id, url);
            }
            catch { }
        }
        return (null, null);
    }

    private async Task<bool> SendFilePartAsync(string id, string? uploadUrl, string fullPath, int? partNumber)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(fullPath);
            return await SendFileBytesAsync(id, uploadUrl, Path.GetFileName(fullPath), ContentTypeFor(fullPath), bytes, partNumber);
        }
        catch { return false; }
    }

    private async Task<bool> SendFileBytesAsync(string id, string? uploadUrl, string filename, string contentType, byte[] bytes, int? partNumber)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(bytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", filename);
            if (partNumber.HasValue)
                form.Add(new StringContent(partNumber.Value.ToString()), "part_number");
            var target = !string.IsNullOrWhiteSpace(uploadUrl) ? uploadUrl! : $"file_uploads/{id}/send";
            var res = await _http.PostAsync(target, form);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task<bool> PollUploadAsync(string id, int maxTries)
    {
        for (var i = 0; i < maxTries; i++)
        {
            try
            {
                var res = await _http.GetAsync($"file_uploads/{id}");
                if (!res.IsSuccessStatusCode) return false;
                using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                if (!doc.RootElement.TryGetProperty("status", out var st)) return false;
                var status = st.GetString();
                if (status == "uploaded") return true;
                if (status == "failed") return false;
                await Task.Delay(1000);
            }
            catch { return false; }
        }
        return false;
    }

    /// <summary>Creates a capture page with REAL file/image blocks.
    /// Order: images first, then other files (including .wav voice
    /// notes), then the note text. Returns (page created, all files
    /// uploaded, uploaded count, failed count). A failed upload never
    /// loses the capture: the page is still created, the failed file
    /// is named in a paragraph, and the local copy is kept.</summary>
    public async Task<(bool PageOk, bool AllFilesOk, int Uploaded, int Failed)> CreateCapturePageWithUploadsAsync(
        string databaseId, string name, string date, string type, string status,
        string tomorrowAction, List<string> tags,
        List<string>? attachmentPaths, string? bodyText)
    {
        var props = new Dictionary<string, object>
        {
            ["Name"] = new { title = new[] { new { text = new { content = name } } } },
            ["Date"] = new { date = new { start = date } },
            ["Type"] = new { select = new { name = type } },
            ["Status"] = new { select = new { name = status } },
            ["Tags"] = new { multi_select = tags.ConvertAll(t => new { name = t }) },
        };
        if (!string.IsNullOrWhiteSpace(tomorrowAction))
            props["Tomorrow's Action"] = new { rich_text = new[] { new { text = new { content = tomorrowAction } } } };

        var images = new List<string>();
        var others = new List<string>();
        foreach (var p in attachmentPaths ?? new List<string>())
            if (IsImagePath(p)) images.Add(p); else others.Add(p);

        var children = new List<object>();
        var uploaded = 0; var failed = 0;
        foreach (var p in images.Concat(others))
        {
            var uploadId = await UploadFileAsync(p);
            if (uploadId != null)
            {
                uploaded++;
                if (IsImagePath(p))
                    children.Add(new { type = "image", image = new { type = "file_upload", file_upload = new { id = uploadId } } });
                else
                    children.Add(new { type = "file", file = new { type = "file_upload", file_upload = new { id = uploadId } } });
            }
            else
            {
                failed++;
                children.Add(new { type = "paragraph", paragraph = new { rich_text = new[] { new { type = "text", text = new { content = "📎 " + Storage.CleanDisplayName(Path.GetFileName(p)) + " (file upload failed — kept locally)" } } } } });
            }
        }
        if (!string.IsNullOrWhiteSpace(bodyText))
            children.Add(new { type = "paragraph", paragraph = new { rich_text = new[] { new { type = "text", text = new { content = bodyText } } } } });

        try
        {
            var payload = new { parent = new { database_id = databaseId }, properties = props, children };
            var res = await _http.PostAsync("pages",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
            return (res.IsSuccessStatusCode, failed == 0, uploaded, failed);
        }
        catch { return (false, false, uploaded, failed); }
    }

    public async Task<bool> CreatePageAsync(
        string databaseId, string name, string date, string type, string status,
        string tomorrowAction, List<string> tags,
        Dictionary<string, List<string>>? sections,
        List<string>? attachmentPaths = null,
        string? bodyText = null)
    {
        try
        {
            var props = new Dictionary<string, object>
            {
                ["Name"] = new { title = new[] { new { text = new { content = name } } } },
                ["Date"] = new { date = new { start = date } },
                ["Type"] = new { select = new { name = type } },
                ["Status"] = new { select = new { name = status } },
                ["Tags"] = new { multi_select = tags.ConvertAll(t => new { name = t }) },
            };
            if (!string.IsNullOrWhiteSpace(tomorrowAction))
                props["Tomorrow's Action"] = new { rich_text = new[] { new { text = new { content = tomorrowAction } } } };

            var children = new List<object>();
            if (sections == null && attachmentPaths != null)
            {
                // Capture page order, fixed: images first, then other
                // files, then the note text. NOTE: this legacy path
                // names attachments only; the capture dialog in v1.0.1
                // uses CreateCapturePageWithUploadsAsync instead, which
                // does true binary upload via /v1/file_uploads. Evening
                // entries (sections != null) never take this path, so
                // their section order is unchanged.
                var images = new List<string>();
                var others = new List<string>();
                foreach (var p in attachmentPaths)
                {
                    var ext = System.IO.Path.GetExtension(p).ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp") images.Add(p);
                    else others.Add(p);
                }
                foreach (var p in images)
                    children.Add(new { type = "paragraph", paragraph = new { rich_text = new[] { new { type = "text", text = new { content = "🖼 " + System.IO.Path.GetFileName(p) } } } } });
                foreach (var p in others)
                    children.Add(new { type = "paragraph", paragraph = new { rich_text = new[] { new { type = "text", text = new { content = "📎 " + System.IO.Path.GetFileName(p) } } } } });
                if (!string.IsNullOrWhiteSpace(bodyText))
                    children.Add(new { type = "paragraph", paragraph = new { rich_text = new[] { new { type = "text", text = new { content = bodyText } } } } });
            }
            if (sections != null)
            {
                foreach (var kv in sections)
                {
                    children.Add(new
                    {
                        type = "heading_2",
                        heading_2 = new { rich_text = new[] { new { type = "text", text = new { content = kv.Key } } } }
                    });
                    // Empty sections stay empty: heading only, no invented bullets.
                    foreach (var line in kv.Value)
                    {
                        children.Add(new
                        {
                            type = "bulleted_list_item",
                            bulleted_list_item = new { rich_text = new[] { new { type = "text", text = new { content = line } } } }
                        });
                    }
                }
            }

            var payload = new { parent = new { database_id = databaseId }, properties = props, children };
            var json = JsonSerializer.Serialize(payload);
            var res = await _http.PostAsync("pages",
                new StringContent(json, Encoding.UTF8, "application/json"));
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Dispose() => _http.Dispose();
}
