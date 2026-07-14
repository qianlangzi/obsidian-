using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InboxDock.Core.Staging;

public sealed class StagingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public StagingStore(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InboxDock",
            "Staging");
    }

    public string RootDirectory { get; }

    public string DataPath => Path.Combine(RootDirectory, "staging.json");

    public string FilesDirectory => Path.Combine(RootDirectory, "files");

    public async Task<StagingLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DataPath)) return new StagingLoadResult(StagingSnapshot.Empty);

        try
        {
            await using var stream = File.OpenRead(DataPath);
            var snapshot = await JsonSerializer.DeserializeAsync<StagingSnapshot>(stream, JsonOptions, cancellationToken);
            return snapshot is null
                ? new StagingLoadResult(StagingSnapshot.Empty, "暂存数据为空，已使用空材料桶。")
                : new StagingLoadResult(Normalize(snapshot));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new StagingLoadResult(StagingSnapshot.Empty, $"无法读取暂存数据：{ex.Message}");
        }
    }

    public async Task SaveAsync(StagingSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(RootDirectory);
        var temporary = DataPath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, Normalize(snapshot), JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporary, DataPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static StagingSnapshot Normalize(StagingSnapshot snapshot) => new(
        snapshot.Items ?? [],
        snapshot.DraftText ?? string.Empty);
}
