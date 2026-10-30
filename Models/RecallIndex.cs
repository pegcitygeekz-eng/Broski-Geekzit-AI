using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChoppaAI.Models;

public sealed class RecallIndex
{
    public string Version { get; set; } = "1.0";
    public List<RecallItem> Entries { get; set; } = [];
    public List<JsonObject> Projects { get; set; } = [];
    public List<JsonObject> Topics { get; set; } = [];
    public List<JsonObject> Conversations { get; set; } = [];
    public List<JsonObject> Notes { get; set; } = [];
    public List<JsonObject> Tasks { get; set; } = [];
    public List<JsonObject> Knowledge { get; set; } = [];
    public JsonObject Keywords { get; set; } = [];
    public JsonArray Relationships { get; set; } = [];
    public JsonArray Pinned { get; set; } = [];
    public JsonArray RecentSearches { get; set; } = [];
    public JsonObject Statistics { get; set; } = [];
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}
