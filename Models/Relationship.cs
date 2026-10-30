namespace ChoppaAI.Models;

public sealed record Relationship(
    string Id,
    string FromType,
    string FromId,
    string ToType,
    string ToId,
    string Type,
    string Timestamp);
