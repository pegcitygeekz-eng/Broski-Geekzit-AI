using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChoppaAI.Models;

public sealed class RuntimeState
{
    public string Version { get; set; } = "1.0";
    public RuntimeSessionState Session { get; set; } = new();
    public RuntimeUserState User { get; set; } = new();
    public RuntimeWorkspaceState Workspace { get; set; } = new();
    public RuntimeContextState Context { get; set; } = new();
    public RuntimeActionsState Actions { get; set; } = new();
    public RuntimeUiState UI { get; set; } = new();
    public RuntimeFilesState Files { get; set; } = new();
    public RuntimeNavigationState Navigation { get; set; } = new();
    public RuntimeFlagsState Flags { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class RuntimeSessionState
{
    public bool Active { get; set; }
    public string SessionId { get; set; } = "";
    public string SessionStart { get; set; } = "";
    public string LastActivity { get; set; } = "";
}

public sealed class RuntimeUserState
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool SpecialUser { get; set; }
}

public sealed class RuntimeWorkspaceState
{
    public string CurrentWorkspace { get; set; } = "Personal";
}

public sealed class RuntimeContextState
{
    public string CurrentProject { get; set; } = "";
    public string CurrentTopic { get; set; } = "";
    public string CurrentConversation { get; set; } = "";
    public string CurrentTask { get; set; } = "";
}

public sealed class RuntimeActionsState
{
    public string LastCommand { get; set; } = "";
    public string LastIntent { get; set; } = "";
    public string LastSearch { get; set; } = "";
    public string LastOpened { get; set; } = "";
}

public sealed class RuntimeUiState
{
    public bool TutorialCompleted { get; set; }
    public string CurrentScreen { get; set; } = "Welcome";
    public bool NotificationsVisible { get; set; } = true;
}

public sealed class RuntimeFilesState
{
    public string CurrentNote { get; set; } = "";
    public string CurrentProjectFile { get; set; } = "";
    public string CurrentConversationFile { get; set; } = "";
}

public sealed class RuntimeNavigationState
{
    public List<string> BackStack { get; set; } = [];
    public List<string> ForwardStack { get; set; } = [];
}

public sealed class RuntimeFlagsState
{
    public bool Busy { get; set; }
    public bool Searching { get; set; }
    public bool Generating { get; set; }
    public bool Learning { get; set; }
}
