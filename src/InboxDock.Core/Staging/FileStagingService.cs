namespace InboxDock.Core.Staging;

public sealed class FileStagingService
{
    private readonly StagingStore store;
    private readonly Func<DateTimeOffset> clock;

    public FileStagingService(StagingStore store, Func<DateTimeOffset>? clock = null)
    {
        this.store = store;
        this.clock = clock ?? (() => DateTimeOffset.Now);
    }

    public async Task<(StagedMaterial Material, StagingSnapshot Snapshot)> StageFilesAsync(
        IReadOnlyList<string> sourcePaths,
        StagingSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        if (sourcePaths.Count == 0) throw new ArgumentException("请至少拖入一个文件。", nameof(sourcePaths));

        var fullPaths = sourcePaths.Select(Path.GetFullPath).ToArray();
        foreach (var source in fullPaths)
        {
            if (Directory.Exists(source)) throw new ArgumentException("暂不支持文件夹，请拖入具体文件。", nameof(sourcePaths));
            if (!File.Exists(source)) throw new FileNotFoundException("要暂存的文件不存在。", source);
        }

        var id = Guid.NewGuid();
        var materialDirectory = Path.Combine(store.FilesDirectory, id.ToString("N"));
        var stagedFiles = new List<StagedFile>(fullPaths.Length);

        try
        {
            Directory.CreateDirectory(materialDirectory);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var source in fullPaths)
            {
                var stagedName = AvailableName(Path.GetFileName(source), usedNames);
                var destination = Path.Combine(materialDirectory, stagedName);
                var temporary = destination + ".copying";

                await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await input.CopyToAsync(output, cancellationToken);
                }

                File.Move(temporary, destination);
                stagedFiles.Add(new StagedFile(
                    source,
                    Path.GetFileName(source),
                    destination,
                    new FileInfo(destination).Length));
            }

            var title = stagedFiles.Count == 1 ? stagedFiles[0].OriginalName : $"{stagedFiles.Count} 个文件";
            var material = new StagedMaterial(
                id,
                StagedMaterialKind.Files,
                title,
                clock(),
                StagedMaterialStatus.AwaitingConfirmation,
                stagedFiles);
            var updated = snapshot with { Items = [.. snapshot.Items, material] };
            await store.SaveAsync(updated, cancellationToken);
            return (material, updated);
        }
        catch
        {
            if (Directory.Exists(materialDirectory)) Directory.Delete(materialDirectory, recursive: true);
            throw;
        }
    }

    private static string AvailableName(string originalName, ISet<string> usedNames)
    {
        if (usedNames.Add(originalName)) return originalName;

        var stem = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{stem} ({suffix}){extension}";
            if (usedNames.Add(candidate)) return candidate;
        }
    }
}
