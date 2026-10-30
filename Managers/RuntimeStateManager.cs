using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChoppaAI.Models;

namespace ChoppaAI.Managers;

internal sealed class RuntimeStateManager
{
    public RuntimeState Read(JsonObject document) =>
        JsonSerializer.Deserialize<RuntimeState>(document.ToJsonString())
        ?? throw new InvalidDataException("The runtime-state document could not be deserialized.");

    public void UpdateContext(
        JsonObject runtimeState,
        JsonObject memory,
        string? projectId = null,
        string? topicId = null,
        string? conversationId = null,
        string? taskId = null)
    {
        var context = GetObject(runtimeState, "Context");
        if (projectId is not null)
        {
            context["CurrentProject"] = projectId;
            memory["CurrentProject"] = projectId;
        }

        if (topicId is not null)
        {
            context["CurrentTopic"] = topicId;
            memory["CurrentTopic"] = topicId;
        }

        if (conversationId is not null)
        {
            context["CurrentConversation"] = conversationId;
            memory["CurrentConversation"] = conversationId;
        }

        if (taskId is not null)
        {
            context["CurrentTask"] = taskId;
            memory["CurrentTask"] = taskId;
        }
    }

    public void StartSession(JsonObject runtimeState, JsonObject memory, string sessionId, bool tutorialCompleted)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        var session = GetObject(runtimeState, "Session");
        session["Active"] = true;
        session["SessionId"] = sessionId;
        session["SessionStart"] = now;
        session["LastActivity"] = now;

        var conversationId = $"CONV-{sessionId}";
        GetObject(runtimeState, "Context")["CurrentConversation"] = conversationId;
        memory["CurrentConversation"] = conversationId;
        memory["SessionStart"] = now;

        var ui = GetObject(runtimeState, "UI");
        ui["TutorialCompleted"] = tutorialCompleted;
        ui["CurrentScreen"] = tutorialCompleted ? "Chat" : "Welcome";
    }

    public void UpdateUser(
        JsonObject runtimeState,
        JsonObject profile,
        JsonObject memory,
        string? displayName,
        string? firstName,
        string? lastName,
        bool specialUser)
    {
        var user = GetObject(runtimeState, "User");
        user["FirstName"] = firstName ?? string.Empty;
        user["LastName"] = lastName ?? string.Empty;
        user["DisplayName"] = displayName ?? string.Empty;
        user["SpecialUser"] = specialUser;
        profile["Name"] = displayName ?? string.Empty;
        memory["CurrentUser"] = displayName ?? string.Empty;
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
}
