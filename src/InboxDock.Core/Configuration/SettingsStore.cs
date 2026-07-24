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

    public string BackupPath => SettingsPath + ".v1.bak";

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return new SettingsLoadResult(null, null);
        }

        string contents;
        try
        {
            contents = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
        }
        catch (IOException exception)
        {
            return new SettingsLoadResult(null, $"配置文件无法打开：{exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(contents))
        {
            return new SettingsLoadResult(null, "配置文件内容为空。");
        }

        var schemaVersion = TryReadSchemaVersion(contents);
        if (schemaVersion is null)
        {
            // 没有 schemaVersion 字段，视为 v1 旧版，执行迁移。
            return await MigrateAsync(contents, cancellationToken);
        }

        if (schemaVersion != AppSettings.CurrentSchemaVersion)
        {
            return new SettingsLoadResult(null, $"不支持的配置版本：{schemaVersion}。");
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(contents, JsonOptions);
            return settings is null
                ? new SettingsLoadResult(null, "配置文件内容为空。")
                : new SettingsLoadResult(settings, null);
        }
        catch (JsonException exception)
        {
            return new SettingsLoadResult(null, $"配置文件无法读取：{exception.Message}");
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

    /// <summary>
    /// 读取 JSON 文档根对象的 schemaVersion 值。缺失或无法解析时返回 null（视为旧版）。
    /// </summary>
    private static int? TryReadSchemaVersion(string contents)
    {
        try
        {
            using var document = JsonDocument.Parse(contents);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            return document.RootElement.TryGetProperty("schemaVersion", out var value) && value.TryGetInt32(out var version)
                ? version
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<SettingsLoadResult> MigrateAsync(string legacyJson, CancellationToken cancellationToken)
    {
        LegacyAppSettings legacy;
        try
        {
            legacy = JsonSerializer.Deserialize<LegacyAppSettings>(legacyJson, JsonOptions)
                ?? throw new JsonException("旧配置内容为空。");
        }
        catch (JsonException exception)
        {
            // 无效 JSON 不被覆盖，也不创建备份。
            return new SettingsLoadResult(null, $"配置文件无法读取：{exception.Message}");
        }

        var migrated = SettingsMigration.Migrate(legacy);
        var validation = migrated.CurrentProfile?.Validate();
        if (validation is null || !validation.IsValid)
        {
            return new SettingsLoadResult(null, $"迁移后的配置无效：{validation?.Message ?? "缺少当前 Vault 配置。"}");
        }

        // 迁移前备份；若备份已存在则不覆盖。
        if (!File.Exists(BackupPath))
        {
            try
            {
                File.Copy(SettingsPath, BackupPath, overwrite: false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new SettingsLoadResult(null, $"无法创建配置备份：{exception.Message}");
            }
        }

        var directory = Path.GetDirectoryName(SettingsPath);
        if (string.IsNullOrEmpty(directory))
        {
            return new SettingsLoadResult(null, "设置文件必须有父目录。");
        }

        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".migration.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, migrated, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            // 重新读取临时文件并验证，确保写入完整后再替换正式文件。
            AppSettings? verified;
            await using (var verifyStream = File.OpenRead(temporaryPath))
            {
                verified = await JsonSerializer.DeserializeAsync<AppSettings>(verifyStream, JsonOptions, cancellationToken);
            }

            if (verified is null || verified.SchemaVersion != AppSettings.CurrentSchemaVersion)
            {
                throw new InvalidOperationException("迁移后的配置重新读取失败。");
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
            return new SettingsLoadResult(migrated, null);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or UnauthorizedAccessException)
        {
            if (File.Exists(temporaryPath) && !Directory.Exists(temporaryPath)) File.Delete(temporaryPath);
            // 迁移失败时保持原始设置和备份不变。
            return new SettingsLoadResult(null, $"配置迁移失败，已保留原始设置：{exception.Message}");
        }
    }
}
