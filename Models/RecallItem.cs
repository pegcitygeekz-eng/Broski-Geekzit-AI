using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

namespace ChoppaAI.Models;

public sealed record RecallItem
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Timestamp { get; init; } = "";
    public List<string> Keywords { get; init; } = [];
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static RecallItem FromJsonObject(JsonObject entry) =>
        new()
        {
            Id = GetString(entry["Id"]),
            Type = GetString(entry["Type"]),
            Title = GetString(entry["Title"]),
            Description = GetString(entry["Description"]),
            Timestamp = GetString(entry["Timestamp"]),
            Keywords = entry["Keywords"] is JsonArray keywords
                ? keywords.Select(GetString).Where(value => value.Length > 0).ToList()
                : [],
            AdditionalData = entry
                .Where(property => property.Key is not ("Id" or "Type" or "Title" or "Description" or "Timestamp" or "Keywords"))
                .ToDictionary(
                    property => property.Key,
                    property => JsonSerializer.SerializeToElement(property.Value),
                    StringComparer.OrdinalIgnoreCase),
        };

    public JsonObject ToJsonObject()
    {
        var keywords = new JsonArray();
        foreach (var keyword in Keywords)
        {
            keywords.Add(keyword);
        }

        var result = new JsonObject
        {
            ["Id"] = Id,
            ["Type"] = Type,
            ["Title"] = Title,
            ["Description"] = Description,
            ["Timestamp"] = Timestamp,
            ["Keywords"] = keywords,
        };
        foreach (var (key, value) in AdditionalData)
        {
            if (result[key] is null)
            {
                result[key] = JsonNode.Parse(value.GetRawText());
            }
        }

        return result;
    }

    private static string GetString(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;
}
