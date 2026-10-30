using System.Text.Json.Nodes;

namespace ChoppaAI.Models;

public sealed class RelationshipStore
{
    public List<Relationship> ProjectRelationships { get; set; } = [];
    public List<Relationship> TopicRelationships { get; set; } = [];
    public List<Relationship> ConversationRelationships { get; set; } = [];
    public List<Relationship> NoteRelationships { get; set; } = [];
    public List<Relationship> TaskRelationships { get; set; } = [];
    public List<Relationship> KnowledgeRelationships { get; set; } = [];
    public List<Relationship> EntityLinks { get; set; } = [];
    public List<string> RelationshipTypes { get; set; } = [];
    public JsonObject Statistics { get; set; } = [];
}
