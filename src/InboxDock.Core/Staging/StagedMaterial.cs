namespace InboxDock.Core.Staging;

public enum StagedMaterialKind
{
    Files,
    Link,
    Text,
}

public enum StagedMaterialStatus
{
    AwaitingConfirmation,
    Deferred,
    Capturing,
    Failed,
}

public sealed record StagedFile(
    string OriginalPath,
    string OriginalName,
    string StagedPath,
    long SizeBytes);

public sealed record StagedMaterial(
    Guid Id,
    StagedMaterialKind Kind,
    string Title,
    DateTimeOffset CreatedAt,
    StagedMaterialStatus Status,
    IReadOnlyList<StagedFile> Files,
    string? Content = null,
    string? LastError = null);
