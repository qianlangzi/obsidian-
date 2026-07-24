using System.Text.Json;
using System.Text.Json.Serialization;

namespace InboxDock.Core.Vault;

/// <summary>
/// 只读发现 Obsidian Vault 的默认附件和 Daily Notes 配置。
/// 只读取 .obsidian 下已知 JSON 文件，不扫描笔记正文，不写入任何文件。
/// 配置缺失、禁用或损坏时返回部分结果，不抛出异常。
/// </summary>
public sealed class VaultDiscovery
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// 发现 Vault 配置。Vault 不存在或不含 .obsidian 时返回 IsValid=false。
    /// </summary>
    public Task<VaultDiscoveryResult> DiscoverAsync(string vaultRoot, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(vaultRoot))
        {
            return Task.FromResult(new VaultDiscoveryResult
            {
                IsValid = false,
                Message = "Vault 路径为空，无法发现配置。",
            });
        }

        if (!Directory.Exists(vaultRoot))
        {
            return Task.FromResult(new VaultDiscoveryResult
            {
                IsValid = false,
                Message = "Vault 文件夹不存在。",
            });
        }

        var obsidianDir = Path.Combine(vaultRoot, ".obsidian");
        if (!Directory.Exists(obsidianDir))
        {
            return Task.FromResult(new VaultDiscoveryResult
            {
                IsValid = false,
                Message = "所选文件夹不是 Obsidian Vault（缺少 .obsidian）。",
            });
        }

        var result = new VaultDiscoveryResult { IsValid = true };

        var appSettings = TryReadJson(Path.Combine(obsidianDir, "app.json"), cancellationToken);
        if (appSettings is not null)
        {
            result = ApplyAttachmentSettings(result, appSettings);
        }

        var dailySettings = TryReadJson(Path.Combine(obsidianDir, "daily-notes.json"), cancellationToken);
        if (dailySettings is not null)
        {
            result = ApplyDailyNotesSettings(result, dailySettings);
        }

        return Task.FromResult(result);
    }

    private static JsonDocument? TryReadJson(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            return JsonDocument.Parse(stream);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 损坏或无法读取的配置不阻断发现，忽略后继续。
            return null;
        }
    }

    private static VaultDiscoveryResult ApplyAttachmentSettings(VaultDiscoveryResult result, JsonDocument document)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return result;

        // newFileLocation: 0=Vault root, 1=specified folder, 2=same folder, 3=subfolder
        if (root.TryGetProperty("newFileLocation", out var locationProp) && locationProp.ValueKind == JsonValueKind.Number)
        {
            result = result with { AttachmentLocationMode = locationProp.GetInt32() };
        }

        // attachmentFolderPath 在模式 1 下是 Vault 相对目录，在模式 3 下是子目录名。
        if (root.TryGetProperty("attachmentFolderPath", out var folderProp) && folderProp.ValueKind == JsonValueKind.String)
        {
            var folder = folderProp.GetString();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                result = result with { AttachmentFolder = folder };
            }
        }

        return result;
    }

    private static VaultDiscoveryResult ApplyDailyNotesSettings(VaultDiscoveryResult result, JsonDocument document)
    {
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return result;

        // Daily Notes 插件默认不启用；若配置文件存在且 folder 非空，视为启用。
        if (root.TryGetProperty("folder", out var folderProp) && folderProp.ValueKind == JsonValueKind.String)
        {
            var folder = folderProp.GetString();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                result = result with { DailyNotesFolder = folder };
            }
        }

        if (root.TryGetProperty("format", out var formatProp) && formatProp.ValueKind == JsonValueKind.String)
        {
            var format = formatProp.GetString();
            if (!string.IsNullOrWhiteSpace(format))
            {
                result = result with { DailyNotesFormat = format };
            }
        }

        if (root.TryGetProperty("template", out var templateProp) && templateProp.ValueKind == JsonValueKind.String)
        {
            var template = templateProp.GetString();
            if (!string.IsNullOrWhiteSpace(template))
            {
                result = result with { DailyNotesTemplate = template };
            }
        }

        return result;
    }
}
