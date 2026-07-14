namespace InboxDock.Core.Staging;

public sealed record StagingSnapshot(IReadOnlyList<StagedMaterial> Items, string DraftText)
{
    public static StagingSnapshot Empty { get; } = new([], string.Empty);
}

public sealed record StagingLoadResult(StagingSnapshot Snapshot, string? Error = null);
