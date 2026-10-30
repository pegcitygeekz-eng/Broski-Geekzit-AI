using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChoppaAI.Managers;
using ChoppaAI.Models;

namespace ChoppaAI;

internal sealed class AiDataMemoryStore
{
    private static readonly string[] ManagedFiles =
    [
        "brain.json",
        "memory.json",
        "knowledge.json",
        "projects.json",
        "topics.json",
        "conversations.json",
        "notes.json",
        "tasks.json",
        "relationships.json",
        "recallindex.json",
        "memorytimeline.json",
        "analytics.json",
        "bookmarks.json",
        "runtimestate.json",
        "specialusers.json",
        "userprofile.json",
        "history.json",
        "recent.json",
        "learning.json",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Dictionary<string, JsonObject> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dirtyFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _dataDirectory;
    private readonly RecallManager _recallManager;
    private readonly RelationshipManager _relationshipManager;
    private readonly RuntimeStateManager _runtimeStateManager = new();

    private AiDataMemoryStore(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
        _recallManager = new RecallManager(() => GetObject("recallindex.json"));
        _relationshipManager = new RelationshipManager(() => GetObject("relationships.json"));
    }

    public List<string> RecoveredFiles { get; } = [];

    public static AiDataMemoryStore Load()
    {
        var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "AIData");
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChoppaAI",
            "AIData");
        Directory.CreateDirectory(dataDirectory);

        var store = new AiDataMemoryStore(dataDirectory);
        foreach (var fileName in ManagedFiles)
        {
            var destinationPath = Path.Combine(dataDirectory, fileName);
            var sourcePath = Path.Combine(sourceDirectory, fileName);
            if (!File.Exists(destinationPath))
            {
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"The AIData template was not found: {fileName}", sourcePath);
                }

                File.Copy(sourcePath, destinationPath);
                store._dirtyFiles.Add(fileName);
            }

            var json = File.ReadAllText(destinationPath);
            if (!TryParseObject(json, out var document))
            {
                var repairedJson = TryRepairJson(json);
                if (repairedJson is not null && TryParseObject(repairedJson, out document))
                {
                    store.RecoveredFiles.Add(fileName);
                    store._documents.Add(fileName, document);
                    store._dirtyFiles.Add(fileName);
                    continue;
                }

                if (!File.Exists(sourcePath))
                {
                    throw new InvalidDataException(
                        $"The AIData file {fileName} is invalid and its recovery template is missing.");
                }

                var backupPath = Path.Combine(
                    dataDirectory,
                    $"{Path.GetFileNameWithoutExtension(fileName)}.invalid-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
                File.Copy(destinationPath, backupPath);
                var template = File.ReadAllText(sourcePath);
                if (!TryParseObject(template, out document))
                {
                    throw new InvalidDataException($"The packaged AIData template {fileName} is invalid.");
                }

                store.RecoveredFiles.Add(fileName);
                store._dirtyFiles.Add(fileName);
            }

            store._documents.Add(fileName, document);
        }

        store.MigrateLegacyDocuments();
        store.RebuildRecallIndex();
        store.SaveAll();
        return store;
    }

    public string? GetDisplayName =>
        NullIfEmpty(GetString(GetObject("runtimestate.json", "User")["DisplayName"]));

    public string? GetFirstName =>
        NullIfEmpty(GetString(GetObject("runtimestate.json", "User")["FirstName"]));

    public string? GetLastName =>
        NullIfEmpty(GetString(GetObject("runtimestate.json", "User")["LastName"]));

    public bool IsSpecialUser =>
        GetBoolean(GetObject("runtimestate.json", "User")["SpecialUser"]);

    public SpecialUserProfile? FindSpecialUser(string firstName, string lastName)
    {
        var specialUsers = GetArray("specialusers.json", "SpecialUsers");
        foreach (var candidate in specialUsers.OfType<JsonObject>())
        {
            if (!string.Equals(
                    GetString(candidate["FirstName"]),
                    firstName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidate["LastNames"] is not JsonArray lastNames
                || !lastNames.Any(name => string.Equals(
                    GetString(name),
                    lastName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            return new SpecialUserProfile(
                GetString(candidate["DisplayName"]),
                GetString(candidate["Nickname"]),
                GetString(candidate["Greeting"]),
                GetString(candidate["FollowUp"]),
                GetBoolean(candidate["SkipNicknamePrompt"]),
                GetBoolean(candidate["SkipTutorial"]),
                GetBoolean(candidate["SkipToMainChat"]));
        }

        return null;
    }

    public void StartSession(
        string? displayName,
        string? firstName,
        string? lastName,
        bool specialUser,
        bool tutorialCompleted)
    {
        _runtimeStateManager.StartSession(
            GetObject("runtimestate.json"),
            GetObject("memory.json"),
            Guid.NewGuid().ToString("N"),
            tutorialCompleted);
        UpdateUser(displayName, firstName, lastName, specialUser);
        Save("runtimestate.json", "memory.json");
    }

    public void UpdateUser(string? displayName, string? firstName, string? lastName, bool specialUser)
    {
        _runtimeStateManager.UpdateUser(
            GetObject("runtimestate.json"),
            GetObject("userprofile.json"),
            GetObject("memory.json"),
            displayName,
            firstName,
            lastName,
            specialUser);
        Save("runtimestate.json", "userprofile.json");
    }

    public void SetTutorialCompleted(bool completed)
    {
        GetObject("runtimestate.json", "UI")["TutorialCompleted"] = completed;
        GetObject("runtimestate.json", "UI")["CurrentScreen"] = completed ? "Chat" : "Welcome";
        Save("runtimestate.json");
    }

    public void UpdateContext(string? projectId = null, string? topicId = null, string? conversationId = null, string? taskId = null)
    {
        _runtimeStateManager.UpdateContext(
            GetObject("runtimestate.json"),
            GetObject("memory.json"),
            projectId,
            topicId,
            conversationId,
            taskId);
        Save("runtimestate.json", "memory.json");
    }

    public void RecordInteraction(string userMessage, string assistantResponse)
    {
        var now = DateTimeOffset.UtcNow;
        var isSensitive = ContainsSensitiveContent(userMessage)
            || ContainsSensitiveContent(assistantResponse);
        var savedUserMessage = isSensitive ? "[Sensitive content omitted]" : userMessage;
        var savedAssistantResponse = isSensitive ? "[Response omitted with sensitive content]" : assistantResponse;
        var session = GetObject("runtimestate.json", "Session");
        session["LastActivity"] = now.ToString("O");
        var actions = GetObject("runtimestate.json", "Actions");
        actions["LastCommand"] = savedUserMessage;
        actions["LastIntent"] = InferIntent(userMessage);

        var memory = GetObject("memory.json");
        memory["LastResponse"] = savedAssistantResponse;
        memory["LastCommand"] = savedUserMessage;
        memory["LastIntent"] = InferIntent(userMessage);
        var sessionMessages = GetArray("memory.json", "SessionMessages");
        sessionMessages.Add(new JsonObject
        {
            ["Timestamp"] = now.ToString("O"),
            ["User"] = savedUserMessage,
            ["Assistant"] = savedAssistantResponse,
        });
        while (sessionMessages.Count > 100)
        {
            sessionMessages.RemoveAt(0);
        }

        var brain = GetObject("brain.json");
        var context = GetObject("brain.json", "Context");
        context["CurrentIntent"] = InferIntent(userMessage);
        var statistics = GetObject("brain.json", "Statistics");
        statistics["MessagesProcessed"] = GetInt(statistics["MessagesProcessed"]) + 1;

        var analytics = GetObject("analytics.json");
        var usage = GetObject("analytics.json", "Usage");
        usage["TotalMessages"] = GetInt(usage["TotalMessages"]) + 1;
        GetArray("history.json", "Interactions").Add(new JsonObject
        {
            ["Timestamp"] = now.ToString("O"),
            ["UserMessage"] = savedUserMessage,
            ["AssistantResponse"] = savedAssistantResponse,
        });

        Save("runtimestate.json", "memory.json", "brain.json", "analytics.json", "history.json");
    }

    public void RecordSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || ContainsSensitiveContent(query))
        {
            return;
        }

        var item = new JsonObject
        {
            ["Query"] = query.Trim(),
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        GetArray("recallindex.json", "RecentSearches").Add(item.DeepClone());
        var recentSearches = GetArray("recallindex.json", "RecentSearches");
        TrimArray(recentSearches, 100);
        var analyticsSearches = GetArray("analytics.json", "SearchHistory");
        analyticsSearches.Add(item);
        TrimArray(analyticsSearches, 250);
        var usage = GetObject("analytics.json", "Usage");
        usage["TotalSearches"] = GetInt(usage["TotalSearches"]) + 1;
        GetObject("runtimestate.json", "Actions")["LastSearch"] = query.Trim();
        Save("recallindex.json", "analytics.json", "runtimestate.json");
    }

    public JsonObject AddEntity(
        string type,
        string title,
        string description,
        IEnumerable<string>? keywords = null,
        string? projectId = null,
        string? topicId = null,
        JsonArray? messages = null,
        string? idOverride = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A memory item needs a title.", nameof(title));
        }

        if (ContainsSensitiveContent(title) || ContainsSensitiveContent(description))
        {
            throw new InvalidDataException("This memory entry contains information that should not be stored.");
        }

        var id = idOverride ?? $"{type.ToUpperInvariant()}-{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var entity = new JsonObject
        {
            ["Id"] = id,
            ["Title"] = title.Trim(),
            ["Description"] = description.Trim(),
            ["Timestamp"] = timestamp,
            ["Keywords"] = ToJsonArray(keywords ?? ExtractKeywords($"{title} {description}")),
        };
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            entity["ProjectId"] = projectId;
        }

        if (!string.IsNullOrWhiteSpace(topicId))
        {
            entity["TopicId"] = topicId;
        }

        if (messages is not null)
        {
            entity["Messages"] = messages.DeepClone();
        }

        switch (type)
        {
            case "Project":
                GetArray("projects.json", "Projects").Add(entity.DeepClone());
                UpdateContext(projectId: id);
                break;
            case "Topic":
                GetArray("topics.json", "Topics").Add(entity.DeepClone());
                UpdateContext(topicId: id);
                break;
            case "Conversation":
                GetArray("conversations.json", "Conversations").Add(entity.DeepClone());
                UpdateContext(conversationId: id);
                break;
            case "Note":
                GetArray("notes.json", "Notes").Add(entity.DeepClone());
                break;
            case "Task":
                GetArray("tasks.json", "Tasks").Add(entity.DeepClone());
                UpdateContext(taskId: id);
                break;
            case "Knowledge":
                GetKnowledgeEntries().Add(entity.DeepClone());
                break;
            case "Bookmark":
                GetArray("bookmarks.json", "Favorites").Add(entity.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported memory entity type.");
        }

        AddRecallEntry(entity, type);
        AddTimelineEvent(type, entity, projectId, topicId);
        AddRelationships(type, id, projectId, topicId);
        AddRecentItem(type, entity);
        AddHistoryItem(type, entity);
        UpdateAnalyticsForEntity(type);
        SaveAll();
        return entity;
    }

    public IReadOnlyList<JsonObject> SearchRecall(string query, int maximumResults = 6, bool recordSearch = true)
    {
        if (recordSearch)
        {
            RecordSearch(query);
        }

        return _recallManager.Search(query, maximumResults);
    }

    public string GetCurrentContextQuery()
    {
        var context = _runtimeStateManager.Read(GetObject("runtimestate.json")).Context;
        var activeIds = new[]
        {
            context.CurrentProject,
            context.CurrentTopic,
            context.CurrentConversation,
            context.CurrentTask,
        }.Where(id => id.Length > 0);
        return _recallManager.GetContextQuery(activeIds);
    }

    public IReadOnlyList<string> GetRelatedTitles(string id)
    {
        return _recallManager.GetTitles(_relationshipManager.GetRelatedIds(id));
    }

    public void EndSession(IReadOnlyList<(string UserMessage, string AssistantResponse)> turns)
    {
        var session = GetObject("runtimestate.json", "Session");
        if (turns.Count > 0)
        {
            var messages = new JsonArray();
            foreach (var turn in turns)
            {
                var sensitiveTurn = ContainsSensitiveContent(turn.UserMessage)
                    || ContainsSensitiveContent(turn.AssistantResponse);
                messages.Add(new JsonObject
                {
                    ["User"] = sensitiveTurn ? "[Sensitive content omitted]" : turn.UserMessage,
                    ["Assistant"] = sensitiveTurn ? "[Response omitted with sensitive content]" : turn.AssistantResponse,
                });
            }

            var firstPrompt = turns[0].UserMessage.Trim();
            if (ContainsSensitiveContent(firstPrompt))
            {
                firstPrompt = "Private conversation";
            }

            var title = firstPrompt.Length > 80 ? $"{firstPrompt[..77].TrimEnd()}..." : firstPrompt;
            var lastResponse = turns[^1].AssistantResponse.Trim();
            var safeLastResponse = ContainsSensitiveContent(lastResponse)
                ? "[Sensitive content omitted]"
                : lastResponse;
            var summary = $"Conversation with {turns.Count} exchange(s). Latest response: {safeLastResponse}";
            var context = GetObject("runtimestate.json", "Context");
            var projectId = GetString(context["CurrentProject"]);
            var topicId = GetString(context["CurrentTopic"]);
            AddEntity(
                "Conversation",
                title,
                summary,
                ExtractKeywords(string.Join(' ', turns
                    .Where(turn => !ContainsSensitiveContent(turn.UserMessage)
                        && !ContainsSensitiveContent(turn.AssistantResponse))
                    .SelectMany(turn =>
                    new[] { turn.UserMessage, turn.AssistantResponse }))),
                projectId,
                topicId,
                messages,
                $"CONV-{GetString(session["SessionId"])}");
        }

        session["Active"] = false;
        session["LastActivity"] = DateTimeOffset.UtcNow.ToString("O");
        GetObject("runtimestate.json", "Flags")["Busy"] = false;
        Save("runtimestate.json");
    }

    public JsonObject? FindRecallEntry(string id)
    {
        var entry = GetArray("recallindex.json", "Entries")
            .OfType<JsonObject>()
            .FirstOrDefault(candidate => string.Equals(
                GetString(candidate["Id"]),
                id,
                StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : (JsonObject)entry.DeepClone();
    }

    public void SetCurrentEntity(JsonObject entity)
    {
        var type = GetString(entity["Type"]);
        var id = GetString(entity["Id"]);
        if (type.Equals("Project", StringComparison.OrdinalIgnoreCase))
        {
            UpdateContext(projectId: id);
        }
        else if (type.Equals("Topic", StringComparison.OrdinalIgnoreCase))
        {
            UpdateContext(topicId: id);
        }
        else if (type.Equals("Conversation", StringComparison.OrdinalIgnoreCase))
        {
            UpdateContext(conversationId: id);
        }
        else if (type.Equals("Task", StringComparison.OrdinalIgnoreCase))
        {
            UpdateContext(taskId: id);
        }
    }

    private void RebuildRecallIndex()
    {
        var index = GetObject("recallindex.json");
        var entriesById = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var existingEntries = GetArray("recallindex.json", "Entries");
        foreach (var existing in existingEntries.OfType<JsonObject>())
        {
            var existingId = GetString(existing["Id"]);
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                entriesById[existingId] = (JsonObject)existing.DeepClone();
            }
        }

        var legacyIndexArrays = new (string Type, string Property)[]
        {
            ("Project", "Projects"),
            ("Topic", "Topics"),
            ("Conversation", "Conversations"),
            ("Note", "Notes"),
            ("Task", "Tasks"),
            ("Knowledge", "Knowledge"),
        };
        foreach (var (type, property) in legacyIndexArrays)
        {
            foreach (var legacy in GetArray("recallindex.json", property).OfType<JsonObject>())
            {
                var legacyId = GetString(legacy["Id"]);
                if (!string.IsNullOrWhiteSpace(legacyId))
                {
                    entriesById[legacyId] = CreateRecallEntry(legacy, type);
                }
            }
        }

        var sources = new (string Type, IEnumerable<JsonObject> Items)[]
        {
            ("Project", GetArray("projects.json", "Projects").OfType<JsonObject>()),
            ("Topic", GetArray("topics.json", "Topics").OfType<JsonObject>()),
            ("Conversation", GetArray("conversations.json", "Conversations").OfType<JsonObject>()),
            ("Note", GetArray("notes.json", "Notes").OfType<JsonObject>()),
            ("Task", GetArray("tasks.json", "Tasks").OfType<JsonObject>()),
            ("Knowledge", GetKnowledgeEntries().OfType<JsonObject>()),
            ("Bookmark", GetArray("bookmarks.json", "Favorites").OfType<JsonObject>()),
        };
        foreach (var (type, items) in sources)
        {
            foreach (var item in items)
            {
                var id = GetString(item["Id"]);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    entriesById[id] = CreateRecallEntry(item, type);
                }
            }
        }

        foreach (var eventEntry in GetArray("memorytimeline.json", "Timeline").OfType<JsonObject>())
        {
            var eventId = GetString(eventEntry["EntityId"]);
            if (string.IsNullOrWhiteSpace(eventId))
            {
                var id = GetString(eventEntry["Id"]);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    entriesById[id] = CreateRecallEntry(eventEntry, GetString(eventEntry["Type"]));
                }
            }
        }

        foreach (var milestone in GetArray("memorytimeline.json", "Milestones").OfType<JsonObject>())
        {
            var id = GetString(milestone["Id"]);
            if (!string.IsNullOrWhiteSpace(id))
            {
                entriesById[id] = CreateRecallEntry(milestone, "Milestone");
            }
        }

        var entries = new JsonArray();
        foreach (var entry in entriesById.Values)
        {
            entries.Add(entry);
        }

        _recallManager.ReplaceEntries(entries.OfType<JsonObject>());
        _dirtyFiles.Add("recallindex.json");
        var statistics = GetObject("recallindex.json", "Statistics");
        statistics["IndexedProjects"] = GetArray("projects.json", "Projects").Count;
        statistics["IndexedTopics"] = GetArray("topics.json", "Topics").Count;
        statistics["IndexedConversations"] = GetArray("conversations.json", "Conversations").Count;
        statistics["IndexedNotes"] = GetArray("notes.json", "Notes").Count;
        statistics["IndexedKnowledge"] = GetKnowledgeEntries().Count;
    }

    private void MigrateLegacyDocuments()
    {
        var runtimeState = GetObject("runtimestate.json");
        if (GetObject(runtimeState, "Migrations")["AiDataConsolidationV1"] is JsonValue marker
            && marker.TryGetValue<bool>(out var completed)
            && completed)
        {
            return;
        }

        var sources = new (string FileName, string TargetFile, string TargetSection)[]
        {
            ("aliases.json", "learning.json", "Aliases"),
            ("commands.json", "learning.json", "Commands"),
            ("corrections.json", "learning.json", "Corrections"),
            ("intents.json", "learning.json", "Intents"),
            ("slang.json", "learning.json", "Slang"),
            ("index.json", "recallindex.json", "LegacyCatalog"),
            ("notifications.json", "runtimestate.json", "Notifications"),
            ("personality.json", "brain.json", "Personality"),
            ("searchprofiles.json", "brain.json", "SearchProfiles"),
            ("workspaces.json", "runtimestate.json", "WorkspaceDefinitions"),
            ("chattemplate.json", "conversations.json", "LegacyTemplate"),
        };
        var existingSources = sources
            .Where(source => File.Exists(Path.Combine(_dataDirectory, source.FileName)))
            .ToArray();

        string? backupDirectory = null;
        if (Directory.EnumerateFiles(_dataDirectory, "*", SearchOption.TopDirectoryOnly).Any())
        {
            backupDirectory = CreateMigrationBackup();
        }

        var parsedSources = new List<(string FileName, string TargetFile, string TargetSection, JsonObject Document)>();
        foreach (var source in existingSources)
        {
            var path = Path.Combine(_dataDirectory, source.FileName);
            var json = File.ReadAllText(path);
            if (!TryParseObject(json, out var document))
            {
                var repaired = TryRepairJson(json);
                if (repaired is null || !TryParseObject(repaired, out document))
                {
                    throw new InvalidDataException(
                        $"The legacy AIData file {source.FileName} is invalid. Its pre-migration backup is preserved at {backupDirectory}.");
                }
            }

            parsedSources.Add((source.FileName, source.TargetFile, source.TargetSection, document));
        }

        foreach (var source in parsedSources)
        {
            var target = GetObject(source.TargetFile);
            if (target[source.TargetSection] is not JsonObject targetSection)
            {
                if (target[source.TargetSection] is not null)
                {
                    throw new InvalidDataException(
                        $"Expected an object at {source.TargetFile}:{source.TargetSection} during AIData migration.");
                }

                targetSection = [];
                target[source.TargetSection] = targetSection;
            }

            MergeJsonObject(targetSection, source.Document, source.FileName, source.TargetSection, target);
        }

        _ = GetObject(runtimeState, "Migrations");
        SaveAll();
        foreach (var fileName in ManagedFiles)
        {
            if (!TryParseObject(File.ReadAllText(Path.Combine(_dataDirectory, fileName)), out _))
            {
                throw new InvalidDataException($"The migrated AIData file failed validation: {fileName}.");
            }
        }

        GetObject(runtimeState, "Migrations")["AiDataConsolidationV1"] = true;
        SaveAll();

        if (backupDirectory is not null)
        {
            var archivedSources = Path.Combine(backupDirectory, "retired-source-files");
            Directory.CreateDirectory(archivedSources);
            foreach (var source in parsedSources)
            {
                var sourcePath = Path.Combine(_dataDirectory, source.FileName);
                if (File.Exists(sourcePath))
                {
                    File.Move(
                        sourcePath,
                        Path.Combine(archivedSources, source.FileName),
                        overwrite: true);
                }
            }
        }
    }

    private string CreateMigrationBackup()
    {
        var applicationDataDirectory = Directory.GetParent(_dataDirectory)?.FullName
            ?? throw new InvalidOperationException("The AIData parent directory could not be determined.");
        var backupsDirectory = Path.Combine(applicationDataDirectory, "backups");
        Directory.CreateDirectory(backupsDirectory);
        var backupDirectory = Path.Combine(
            backupsDirectory,
            $"AIData-before-0.0.3-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupDirectory);
        foreach (var file in Directory.EnumerateFiles(_dataDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(file, Path.Combine(backupDirectory, Path.GetFileName(file)));
        }

        return backupDirectory;
    }

    private static void MergeJsonObject(
        JsonObject target,
        JsonObject source,
        string sourceFile,
        string targetSection,
        JsonObject targetDocument)
    {
        foreach (var (propertyName, sourceValue) in source)
        {
            if (!target.TryGetPropertyValue(propertyName, out var targetValue))
            {
                target[propertyName] = sourceValue?.DeepClone();
                continue;
            }

            if (targetValue is JsonObject targetObject && sourceValue is JsonObject sourceObject)
            {
                MergeJsonObject(
                    targetObject,
                    sourceObject,
                    sourceFile,
                    $"{targetSection}.{propertyName}",
                    targetDocument);
                continue;
            }

            if (targetValue is JsonArray targetArray && sourceValue is JsonArray sourceArray)
            {
                foreach (var item in sourceArray)
                {
                    var serialized = item?.ToJsonString();
                    if (!targetArray.Any(existing => existing?.ToJsonString() == serialized))
                    {
                        targetArray.Add(item?.DeepClone());
                    }
                }

                continue;
            }

            if (targetValue?.ToJsonString() == sourceValue?.ToJsonString())
            {
                continue;
            }

            var conflicts = targetDocument["MigrationConflicts"] as JsonArray ?? [];
            targetDocument["MigrationConflicts"] = conflicts;
            conflicts.Add(new JsonObject
            {
                ["SourceFile"] = sourceFile,
                ["Path"] = $"{targetSection}.{propertyName}",
                ["Value"] = sourceValue?.DeepClone(),
            });
        }
    }

    private static JsonObject GetObject(JsonObject parent, string propertyName)
    {
        if (parent[propertyName] is JsonObject value)
        {
            return value;
        }

        if (parent[propertyName] is not null)
        {
            throw new InvalidDataException($"Expected an object at runtime state:{propertyName}.");
        }

        value = [];
        parent[propertyName] = value;
        return value;
    }

    private void AddRecallEntry(JsonObject entity, string type)
    {
        var recallEntry = CreateRecallEntry(entity, type);
        _recallManager.AddOrUpdate(recallEntry, type);
    }

    private static JsonObject CreateRecallEntry(JsonObject entity, string type)
    {
        var result = new JsonObject
        {
            ["Id"] = GetString(entity["Id"]),
            ["Type"] = type,
            ["Title"] = GetString(entity["Title"]),
            ["Description"] = GetString(entity["Description"]),
            ["Timestamp"] = GetString(entity["Timestamp"]).Length > 0
                ? GetString(entity["Timestamp"])
                : GetString(entity["Date"]),
        };
        if (entity["Keywords"] is JsonArray keywords)
        {
            result["Keywords"] = keywords.DeepClone();
        }

        return result;
    }

    private void AddTimelineEvent(string type, JsonObject entity, string? projectId, string? topicId)
    {
        var eventType = type switch
        {
            "Project" => "ProjectCreated",
            "Topic" => "TopicCreated",
            "Conversation" => "Conversation",
            "Task" => "TaskCreated",
            "Bookmark" => "BookmarkAdded",
            "Knowledge" => "KnowledgeAdded",
            "Note" => "NoteCreated",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        var id = $"EVT-{Guid.NewGuid():N}";
        var timelineEvent = new JsonObject
        {
            ["Id"] = id,
            ["Timestamp"] = GetString(entity["Timestamp"]),
            ["Type"] = eventType,
            ["Title"] = GetString(entity["Title"]),
            ["Description"] = GetString(entity["Description"]),
            ["EntityId"] = GetString(entity["Id"]),
            ["EntityType"] = type,
            ["Keywords"] = entity["Keywords"]?.DeepClone(),
        };
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            timelineEvent["ProjectId"] = projectId;
        }

        if (!string.IsNullOrWhiteSpace(topicId))
        {
            timelineEvent["TopicId"] = topicId;
        }

        GetArray("memorytimeline.json", "Timeline").Add(timelineEvent.DeepClone());
        GetArray("memorytimeline.json", "RecentEvents").Add(timelineEvent.DeepClone());
        TrimArray(GetArray("memorytimeline.json", "RecentEvents"), 100);
        var historyProperty = type switch
        {
            "Project" => "ProjectHistory",
            "Topic" => "TopicHistory",
            "Conversation" => "ConversationHistory",
            "Task" => "TaskHistory",
            "Knowledge" => "KnowledgeHistory",
            _ => null,
        };
        if (historyProperty is not null)
        {
            GetArray("memorytimeline.json", historyProperty).Add(timelineEvent.DeepClone());
        }

        var statistics = GetObject("memorytimeline.json", "Statistics");
        statistics["EventsTracked"] = GetInt(statistics["EventsTracked"]) + 1;
        if (type == "Project")
        {
            statistics["ProjectsTracked"] = GetInt(statistics["ProjectsTracked"]) + 1;
        }
        else if (type == "Topic")
        {
            statistics["TopicsTracked"] = GetInt(statistics["TopicsTracked"]) + 1;
        }
        else if (type == "Conversation")
        {
            statistics["ConversationsTracked"] = GetInt(statistics["ConversationsTracked"]) + 1;
        }
        else if (type == "Task")
        {
            statistics["TasksTracked"] = GetInt(statistics["TasksTracked"]) + 1;
        }
        else if (type == "Knowledge")
        {
            statistics["KnowledgeEntriesTracked"] = GetInt(statistics["KnowledgeEntriesTracked"]) + 1;
        }

        RefreshTimelineBuckets();
    }

    private void AddRelationships(string type, string id, string? projectId, string? topicId)
    {
        var runtimeContext = GetObject("runtimestate.json", "Context");
        var currentProjectId = projectId ?? GetString(runtimeContext["CurrentProject"]);
        var currentTopicId = topicId ?? GetString(runtimeContext["CurrentTopic"]);
        var currentConversationId = GetString(runtimeContext["CurrentConversation"]);

        if (type == "Topic" && !string.IsNullOrWhiteSpace(currentProjectId))
        {
            AddRelationship("Project", currentProjectId, "Topic", id, "BelongsTo");
        }
        else if (type == "Conversation")
        {
            if (!string.IsNullOrWhiteSpace(currentTopicId))
            {
                AddRelationship("Topic", currentTopicId, "Conversation", id, "MentionedIn");
            }

            if (!string.IsNullOrWhiteSpace(currentProjectId))
            {
                AddRelationship("Project", currentProjectId, "Conversation", id, "BelongsTo");
            }
        }
        else if (type == "Note" && !string.IsNullOrWhiteSpace(currentConversationId))
        {
            AddRelationship("Conversation", currentConversationId, "Note", id, "References");
        }
        else if (type == "Task" && !string.IsNullOrWhiteSpace(currentProjectId))
        {
            AddRelationship("Project", currentProjectId, "Task", id, "Contains");
        }
        else if (type == "Knowledge" && !string.IsNullOrWhiteSpace(currentTopicId))
        {
            AddRelationship("Topic", currentTopicId, "Knowledge", id, "References");
        }
    }

    private void AddRelationship(string fromType, string fromId, string toType, string toId, string relationType)
    {
        var typedArray = (fromType, toType) switch
        {
            ("Project", "Topic") => "ProjectRelationships",
            ("Topic", "Conversation") => "TopicRelationships",
            ("Conversation", "Note") => "ConversationRelationships",
            ("Project", "Task") => "TaskRelationships",
            ("Topic", "Knowledge") => "KnowledgeRelationships",
            ("Project", "Conversation") => "ProjectRelationships",
            _ => "EntityLinks",
        };
        _relationshipManager.Add(
            new Relationship(
                $"REL-{Guid.NewGuid():N}",
                fromType,
                fromId,
                toType,
                toId,
                relationType,
                DateTimeOffset.UtcNow.ToString("O")),
            typedArray);
    }

    private void AddRecentItem(string type, JsonObject entity)
    {
        var key = type switch
        {
            "Project" => "RecentProjects",
            "Topic" => "RecentTopics",
            "Conversation" => "RecentConversations",
            "Note" => "RecentNotes",
            "Task" => "RecentTasks",
            "Knowledge" => "RecentlyLearned",
            _ => null,
        };
        if (key is null)
        {
            return;
        }

        var item = new JsonObject
        {
            ["Id"] = GetString(entity["Id"]),
            ["Title"] = GetString(entity["Title"]),
            ["Timestamp"] = GetString(entity["Timestamp"]),
        };
        var items = GetArray("recent.json", key);
        items.Insert(0, item);
        while (items.Count > 100)
        {
            items.RemoveAt(items.Count - 1);
        }
    }

    private void RefreshTimelineBuckets()
    {
        var now = DateTimeOffset.UtcNow;
        var events = GetArray("memorytimeline.json", "Timeline")
            .OfType<JsonObject>()
            .Where(item => DateTimeOffset.TryParse(GetString(item["Timestamp"]), out var timestamp)
                && timestamp.UtcDateTime.Date <= now.UtcDateTime.Date)
            .ToArray();
        var today = now.UtcDateTime.Date;
        var weekStart = today.AddDays(-((int)today.DayOfWeek + 6) % 7);
        SetTimelineBucket("Today", events.Where(item => ParseTimestamp(item)?.UtcDateTime.Date == today));
        SetTimelineBucket("ThisWeek", events.Where(item =>
        {
            var date = ParseTimestamp(item)?.UtcDateTime.Date;
            return date is not null && date >= weekStart && date <= today;
        }));
        SetTimelineBucket("ThisMonth", events.Where(item =>
        {
            var date = ParseTimestamp(item)?.UtcDateTime.Date;
            return date is not null && date.Value.Year == today.Year && date.Value.Month == today.Month;
        }));
        SetTimelineBucket("ThisYear", events.Where(item => ParseTimestamp(item)?.UtcDateTime.Year == today.Year));
    }

    private void SetTimelineBucket(string propertyName, IEnumerable<JsonObject> items)
    {
        var bucket = new JsonArray();
        foreach (var item in items)
        {
            bucket.Add(item.DeepClone());
        }

        GetObject("memorytimeline.json")[propertyName] = bucket;
    }

    private static DateTimeOffset? ParseTimestamp(JsonObject item) =>
        DateTimeOffset.TryParse(GetString(item["Timestamp"]), out var timestamp)
            ? timestamp
            : null;

    private void AddHistoryItem(string type, JsonObject entity)
    {
        var key = type switch
        {
            "Project" => "Projects",
            "Topic" => "Topics",
            "Conversation" => "Sessions",
            "Task" => "Commands",
            _ => null,
        };
        if (key is null)
        {
            return;
        }

        GetArray("history.json", key).Add(new JsonObject
        {
            ["Id"] = GetString(entity["Id"]),
            ["Title"] = GetString(entity["Title"]),
            ["Timestamp"] = GetString(entity["Timestamp"]),
        });
    }

    private void UpdateAnalyticsForEntity(string type)
    {
        var usage = GetObject("analytics.json", "Usage");
        var achievementKey = type switch
        {
            "Project" => "ProjectsCreated",
            "Topic" => "TopicsCreated",
            "Conversation" => "ConversationsArchived",
            "Knowledge" => "KnowledgeEntriesAdded",
            "Note" => "TotalNotesCreated",
            "Task" => "TotalTasksCreated",
            "Bookmark" => "TotalBookmarksAdded",
            _ => null,
        };
        if (achievementKey is null)
        {
            return;
        }

        var stats = type is "Project" or "Topic" or "Conversation" or "Knowledge"
            ? GetObject("analytics.json", "Achievements")
            : usage;
        stats[achievementKey] = GetInt(stats[achievementKey]) + 1;
    }

    private JsonObject GetObject(string fileName, params string[] path)
    {
        if (!_documents.TryGetValue(fileName, out var document))
        {
            throw new InvalidOperationException($"The AIData file is not loaded: {fileName}");
        }

        JsonObject current = document;
        foreach (var key in path)
        {
            if (current[key] is not JsonObject nested)
            {
                if (current[key] is not null)
                {
                    throw new InvalidDataException($"Expected an object at {fileName}:{key}.");
                }

                nested = new JsonObject();
                current[key] = nested;
                _dirtyFiles.Add(fileName);
            }

            current = nested;
        }

        return current;
    }

    private JsonArray GetArray(string fileName, string property)
    {
        var document = GetObject(fileName);
        if (document[property] is JsonArray array)
        {
            return array;
        }

        if (document[property] is not null)
        {
            throw new InvalidDataException($"Expected an array at {fileName}:{property}.");
        }

        array = [];
        document[property] = array;
        _dirtyFiles.Add(fileName);
        return array;
    }

    private JsonArray GetKnowledgeEntries()
    {
        var knowledge = GetObject("knowledge.json", "Knowledge");
        if (knowledge["Entries"] is JsonArray entries)
        {
            return entries;
        }

        if (knowledge["Entries"] is not null)
        {
            throw new InvalidDataException("Expected an array at knowledge.json:Knowledge.Entries.");
        }

        entries = [];
        knowledge["Entries"] = entries;
        _dirtyFiles.Add("knowledge.json");
        return entries;
    }

    private void Save(params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            _dirtyFiles.Add(fileName);
        }

        SaveAll();
    }

    private void SaveAll()
    {
        foreach (var fileName in ManagedFiles)
        {
            var path = Path.Combine(_dataDirectory, fileName);
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, _documents[fileName].ToJsonString(JsonOptions));
                File.Move(temporaryPath, path, overwrite: true);
                _dirtyFiles.Remove(fileName);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    private static bool TryParseObject(string json, out JsonObject document)
    {
        document = null!;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject parsed)
            {
                return false;
            }

            document = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? TryRepairJson(string json)
    {
        var text = json.Trim().TrimStart('\uFEFF');
        if (text.StartsWith("@'", StringComparison.Ordinal))
        {
            text = text[2..].TrimStart();
        }

        if (text.EndsWith("'@", StringComparison.Ordinal))
        {
            text = text[..^2].TrimEnd();
        }

        var start = text.IndexOfAny(['{', '[']);
        var end = Math.Max(text.LastIndexOf('}'), text.LastIndexOf(']'));
        if (start < 0 || end < start)
        {
            return null;
        }

        return text[start..(end + 1)];
    }

    private static string InferIntent(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        if (normalized.Contains("project", StringComparison.Ordinal))
        {
            return "Project";
        }

        if (normalized.Contains("topic", StringComparison.Ordinal))
        {
            return "Topic";
        }

        if (normalized.Contains("remember", StringComparison.Ordinal))
        {
            return "Remember";
        }

        if (normalized.Contains("search", StringComparison.Ordinal)
            || normalized.Contains("find", StringComparison.Ordinal)
            || normalized.Contains("look up", StringComparison.Ordinal))
        {
            return "Search";
        }

        return "Conversation";
    }

    private static bool ContainsSensitiveContent(string text)
    {
        string[] sensitiveMarkers =
        [
            "password", "passcode", "one-time code", "verification code", "secret",
            "api key", "access token", "private key", "social security", "ssn",
            "imei", "serial number", "credit card", "bank account", "medical record",
            "diagnosis", "prescription",
        ];
        return sensitiveMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b")
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b\d{8,}\b");
    }

    private static IEnumerable<string> ExtractKeywords(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]{3,}")
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> GetArrayValues(JsonNode? node) =>
        node is JsonArray array ? array.Select(GetString) : [];

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var result = new JsonArray();
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result.Add(value);
        }

        return result;
    }

    private static int GetInt(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var number) ? number : 0;

    private static bool GetBoolean(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var result) && result;

    private static string GetString(JsonNode? value) =>
        value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static void TrimArray(JsonArray array, int maximumCount)
    {
        while (array.Count > maximumCount)
        {
            array.RemoveAt(0);
        }
    }

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

internal sealed record SpecialUserProfile(
    string DisplayName,
    string Nickname,
    string Greeting,
    string FollowUp,
    bool SkipNicknamePrompt,
    bool SkipTutorial,
    bool SkipToMainChat);
