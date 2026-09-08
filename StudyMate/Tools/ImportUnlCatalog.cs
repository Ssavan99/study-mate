using System.Text.Json;

namespace StudyMate.Tools;

/// <summary>One-shot import utility; call from a temporary maintenance host, never a request path.</summary>
public static class ImportUnlCatalog
{
    public static async Task WriteAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient(); client.DefaultRequestHeaders.UserAgent.ParseAdd("StudyMate-catalog-import/1.0");
        using var subjectsDocument = JsonDocument.Parse(await client.GetStringAsync("https://bulletin.unl.edu/courses.json", cancellationToken));
        var departments = new List<object>();
        foreach (var subject in subjectsDocument.RootElement.EnumerateObject())
        {
            try
            {
                await Task.Delay(200, cancellationToken);
                using var coursesDocument = JsonDocument.Parse(await client.GetStringAsync($"https://bulletin.unl.edu/courses/{subject.Name}.json", cancellationToken));
                var courses = new List<object>();
                if (coursesDocument.RootElement.TryGetProperty("courses", out var rows)) foreach (var row in rows.EnumerateArray())
                {
                    if (!row.TryGetProperty("courseCodes", out var codes) || !row.TryGetProperty("title", out var title)) continue;
                    var home = codes.EnumerateArray().FirstOrDefault(c => c.TryGetProperty("@type", out var type) && type.GetString() == "home listing");
                    if (home.ValueKind == JsonValueKind.Undefined || !home.TryGetProperty("courseNumber", out var number)) continue;
                    courses.Add(new { code = subject.Name + " " + number.GetString(), title = title.GetString() });
                }
                departments.Add(new { code = subject.Name, title = subject.Value.GetProperty("title").GetString(), courses });
            }
            catch (HttpRequestException) { /* public endpoint may omit a subject; skip it */ }
        }
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { departments }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }
}
