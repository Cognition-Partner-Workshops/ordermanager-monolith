using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OrderManager.Api.CharacterizationTests;

internal static class GoldenFileAssertions
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static async Task AssertMatchesAsync(HttpResponseMessage response, string fileName)
    {
        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);
        Normalize(node);
        var normalized = JsonSerializer.Serialize(node, SerializerOptions);
        var goldenPath = Path.Combine(
            AppContext.BaseDirectory,
            "CharacterizationTests",
            "golden",
            fileName);

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            var sourcePath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "../../../CharacterizationTests/golden",
                fileName));
            File.WriteAllText(sourcePath, normalized);
            return;
        }

        var expected = await File.ReadAllTextAsync(goldenPath);
        Assert.Equal(expected, normalized);
    }

    private static void Normalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject.ToList())
                {
                    if (property.Key is "createdAt" or "lastRestocked" or "traceId")
                    {
                        jsonObject[property.Key] = "<timestamp>";
                    }
                    else
                    {
                        Normalize(property.Value);
                    }
                }

                break;
            case JsonArray jsonArray:
                foreach (var child in jsonArray)
                {
                    Normalize(child);
                }

                break;
        }
    }
}
