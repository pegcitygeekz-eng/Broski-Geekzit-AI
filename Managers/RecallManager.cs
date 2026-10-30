using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ChoppaAI.Models;

namespace ChoppaAI.Managers;

internal sealed class RecallManager(Func<JsonObject> getDocument)
{
    public void AddOrUpdate(JsonObject entity, string type)
    {
        var item = RecallItem.FromJsonObject(entity) with { Type = type };
        var entries = GetEntries();
        entries.RemoveAll(candidate => candidate.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase));
        entries.Add(item);
        WriteEntries(entries);
        RefreshCounts(entries);
    }

    public void ReplaceEntries(IEnumerable<JsonObject> entries)
    {
        var items = entries
            .Select(RecallItem.FromJsonObject)
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToList();
        WriteEntries(items);
        RefreshCounts(items);
    }

    public IReadOnlyList<JsonObject> Search(string query, int maximumResults)
    {
        var terms = ExtractKeywords(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (terms.Count == 0 || maximumResults <= 0)
        {
            return [];
        }

        return GetEntries()
            .Select(entry =>
            {
                var searchable = string.Join(' ', entry.Title, entry.Description, string.Join(' ', entry.Keywords));
                var words = ExtractKeywords(searchable).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return (Entry: entry, Score: terms.Count(words.Contains));
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenByDescending(result => result.Entry.Timestamp)
            .Take(maximumResults)
            .Select(result => result.Entry.ToJsonObject())
            .ToArray();
    }

    public string GetContextQuery(IEnumerable<string> activeIds) =>
        string.Join(
            ' ',
            GetEntries()
                .Where(entry => activeIds.Contains(entry.Id, StringComparer.OrdinalIgnoreCase))
                .Select(entry => entry.Title));

    public IReadOnlyList<string> GetTitles(IEnumerable<string> ids)
    {
        var idSet = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GetEntries()
            .Where(entry => idSet.Contains(entry.Id))
            .Select(entry => entry.Title)
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private List<RecallItem> GetEntries()
    {
        var document = getDocument();
        if (document["Entries"] is not JsonArray entries)
        {
            if (document["Entries"] is not null)
            {
                throw new InvalidDataException("Expected an array at recallindex.json:Entries.");
            }

            entries = [];
            document["Entries"] = entries;
        }

        var model = new RecallIndex
        {
            Entries = entries.OfType<JsonObject>().Select(RecallItem.FromJsonObject).ToList(),
        };
        return model.Entries;
    }

    private void WriteEntries(IEnumerable<RecallItem> items)
    {
        var model = new RecallIndex { Entries = items.ToList() };
        var entries = new JsonArray();
        foreach (var item in model.Entries)
        {
            entries.Add(item.ToJsonObject());
        }

        getDocument()["Entries"] = entries;
    }

    private void RefreshCounts(IReadOnlyCollection<RecallItem> entries)
    {
        var document = getDocument();
        var statistics = document["Statistics"] as JsonObject ?? [];
        document["Statistics"] = statistics;
        foreach (var type in new[] { "Project", "Topic", "Conversation", "Note", "Task", "Knowledge", "Bookmark" })
        {
            statistics[$"Indexed{Pluralize(type)}"] = entries.Count(entry =>
                entry.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<string> ExtractKeywords(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]{3,}")
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static string Pluralize(string type) => type switch
    {
        "Project" => "Projects",
        "Topic" => "Topics",
        "Conversation" => "Conversations",
        "Note" => "Notes",
        "Task" => "Tasks",
        "Knowledge" => "Knowledge",
        _ => "Bookmarks",
    };
}
