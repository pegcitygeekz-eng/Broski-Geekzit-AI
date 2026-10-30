using System.IO;
using System.Text.Json.Nodes;
using ChoppaAI.Models;

namespace ChoppaAI.Managers;

internal sealed class RelationshipManager(Func<JsonObject> getDocument)
{
    private static readonly string[] RelationshipArrays =
    [
        "ProjectRelationships",
        "TopicRelationships",
        "ConversationRelationships",
        "NoteRelationships",
        "TaskRelationships",
        "KnowledgeRelationships",
        "EntityLinks",
    ];

    public void Add(Relationship relationship, string typedArrayName)
    {
        var document = getDocument();
        var typedArray = GetArray(document, typedArrayName);
        typedArray.Add(ToJsonObject(relationship));
        if (!typedArrayName.Equals("EntityLinks", StringComparison.Ordinal))
        {
            GetArray(document, "EntityLinks").Add(ToJsonObject(relationship));
        }

        var statistics = document["Statistics"] as JsonObject ?? [];
        document["Statistics"] = statistics;
        Increment(statistics, "TotalRelationships");
        var statName = typedArrayName == "EntityLinks"
            ? "Total"
            : typedArrayName.Replace("Relationships", "Links", StringComparison.Ordinal);
        Increment(statistics, statName);
    }

    public IReadOnlyList<string> GetRelatedIds(string id)
    {
        var store = new RelationshipStore();
        var document = getDocument();
        foreach (var propertyName in RelationshipArrays)
        {
            if (document[propertyName] is not JsonArray array)
            {
                if (document[propertyName] is not null)
                {
                    throw new InvalidDataException($"Expected an array at relationships.json:{propertyName}.");
                }

                continue;
            }

            var relationships = array.OfType<JsonObject>().Select(FromJsonObject).ToList();
            switch (propertyName)
            {
                case "ProjectRelationships":
                    store.ProjectRelationships = relationships;
                    break;
                case "TopicRelationships":
                    store.TopicRelationships = relationships;
                    break;
                case "ConversationRelationships":
                    store.ConversationRelationships = relationships;
                    break;
                case "NoteRelationships":
                    store.NoteRelationships = relationships;
                    break;
                case "TaskRelationships":
                    store.TaskRelationships = relationships;
                    break;
                case "KnowledgeRelationships":
                    store.KnowledgeRelationships = relationships;
                    break;
                case "EntityLinks":
                    store.EntityLinks = relationships;
                    break;
            }
        }

        return store.ProjectRelationships
            .Concat(store.TopicRelationships)
            .Concat(store.ConversationRelationships)
            .Concat(store.NoteRelationships)
            .Concat(store.TaskRelationships)
            .Concat(store.KnowledgeRelationships)
            .Concat(store.EntityLinks)
            .Where(link => link.FromId.Equals(id, StringComparison.OrdinalIgnoreCase)
                || link.ToId.Equals(id, StringComparison.OrdinalIgnoreCase))
            .Select(link => link.FromId.Equals(id, StringComparison.OrdinalIgnoreCase)
                ? link.ToId
                : link.FromId)
            .Where(linkedId => !string.IsNullOrWhiteSpace(linkedId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static JsonArray GetArray(JsonObject document, string propertyName)
    {
        if (document[propertyName] is JsonArray array)
        {
            return array;
        }

        if (document[propertyName] is not null)
        {
            throw new InvalidDataException($"Expected an array at relationships.json:{propertyName}.");
        }

        array = [];
        document[propertyName] = array;
        return array;
    }

    private static JsonObject ToJsonObject(Relationship relationship) =>
        new()
        {
            ["Id"] = relationship.Id,
            ["FromType"] = relationship.FromType,
            ["FromId"] = relationship.FromId,
            ["ToType"] = relationship.ToType,
            ["ToId"] = relationship.ToId,
            ["Type"] = relationship.Type,
            ["Timestamp"] = relationship.Timestamp,
        };

    private static Relationship FromJsonObject(JsonObject value) =>
        new(
            GetString(value["Id"]),
            GetString(value["FromType"]),
            GetString(value["FromId"]),
            GetString(value["ToType"]),
            GetString(value["ToId"]),
            GetString(value["Type"]),
            GetString(value["Timestamp"]));

    private static string GetString(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;

    private static void Increment(JsonObject statistics, string key)
    {
        var count = statistics[key] is JsonValue value && value.TryGetValue<int>(out var current)
            ? current
            : 0;
        statistics[key] = count + 1;
    }
}
