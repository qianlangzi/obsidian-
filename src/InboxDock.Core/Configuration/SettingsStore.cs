using System.Text.Json;
using System.Text.Json.Serialization;

namespace InboxDock.Core.Configuration;

public sealed record SettingsLoadResult(AppSettings? Settings, string? ErrorMessage)
{
    public bool IsSuccess => Settings is not null && ErrorMessage is null;
}

public sealed class SettingsStore(string? settingsPath = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string SettingsPath { get; } = settingsPath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "settings.json");

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return new SettingsLoadResult(null, null);
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            return settings is null
                ? new SettingsLoadResult(null, "配置文件内容为空。")
                : new SettingsLoadResult(settings, null);
        }
        catch (JsonException exception)
        {
            return new SettingsLoadResult(null, $"配置文件无法读取：{exception.Message}");
        }
        catch (IOException exception)
        {
            return new SettingsLoadResult(null, $"配置文件无法打开：{exception.Message}");
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(SettingsPath)
            ?? throw new InvalidOperationException("设置文件必须有父目录。");
        Directory.CreateDirectory(directory);

        var temporaryPath = SettingsPath + ".tmp";
        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }
}
