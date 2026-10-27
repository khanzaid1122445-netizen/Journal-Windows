using System;
using System.Collections.Generic;
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

    public async Task<bool> CreatePageAsync(
        string databaseId, string name, string date, string type, string status,
        string tomorrowAction, List<string> tags,
        Dictionary<string, List<string>>? sections)
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
