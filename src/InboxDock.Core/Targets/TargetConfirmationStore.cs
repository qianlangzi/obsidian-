using System.Text.Json;
using System.Text.Json.Serialization;

namespace InboxDock.Core.Targets;

/// <summary>
/// 持久化每个目标最近一次被用户确认写入时的 Revision，
/// 用于 <see cref="CapturePreviewService"/> 判断是否需要再次显示预览。
/// 同时记录每种材料类型最近使用的目标 Id，用于默认选中。
/// </summary>
public sealed class TargetConfirmationStore(string? storePath = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string StorePath { get; } = storePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "target-confirmations.json");

    /// <summary>读取已确认的目标 Revision 表与材料类型偏好。文件缺失返回空记录。</summary>
    public async Task<TargetConfirmationRecord> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(StorePath)) return TargetConfirmationRecord.Empty;

        try
        {
            var contents = await File.ReadAllTextAsync(StorePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(contents)) return TargetConfirmationRecord.Empty;

            var record = JsonSerializer.Deserialize<TargetConfirmationRecord>(contents, JsonOptions);
            return record ?? TargetConfirmationRecord.Empty;
        }
        catch (JsonException)
        {
            return TargetConfirmationRecord.Empty;
        }
        catch (IOException)
        {
            return TargetConfirmationRecord.Empty;
        }
    }

    /// <summary>原子写入确认记录。</summary>
    public async Task SaveAsync(TargetConfirmationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var directory = Path.GetDirectoryName(StorePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temporaryPath = StorePath + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, StorePath, overwrite: true);
    }
}

/// <summary>持久化的目标确认状态。</summary>
public sealed record TargetConfirmationRecord
{
    /// <summary>目标 Id 到最近确认 Revision 的映射。缺失表示从未确认。</summary>
    public IReadOnlyDictionary<Guid, int> ConfirmedRevisions { get; init; } = new Dictionary<Guid, int>();

    /// <summary>材料类型（整数）到最近使用目标 Id 的映射。</summary>
    public IReadOnlyDictionary<int, Guid> LastUsedTargets { get; init; } = new Dictionary<int, Guid>();

    public static TargetConfirmationRecord Empty { get; } = new();
}
