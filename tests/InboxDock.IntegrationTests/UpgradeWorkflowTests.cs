using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using InboxDock.Core.Configuration;
using InboxDock.Core.Staging;
using InboxDock.Core.Targets;

namespace InboxDock.IntegrationTests;

/// <summary>
/// 端到端升级测试：模拟从 v0.2 旧设置和旧暂存 JSON 升级到 v0.3.0 的完整流程。
/// 确保旧用户升级后设置和暂存材料不丢失。
/// </summary>
public sealed class UpgradeWorkflowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public async Task Upgrade_FromV1SettingsAndStaging_AllDataPreserved()
    {
        using var temp = new TemporaryDirectory();
        var settingsPath = Path.Combine(temp.Path, "settings.json");

        // 1. 写入旧版 settings.json（使用 LegacyAppSettings 序列化保证结构一致）
        var legacy = new LegacyAppSettings
        {
            VaultPath = "E:/知识库/测试仓库",
            InboxPath = "00 Inbox收件箱",
            DailyPath = "01 Daily日常",
            DailyTemplatePath = "10 Knowledge Hub/Templates/Daily.md",
            AttachmentsPath = "05 Resources/Attachments",
            AlwaysOnTop = true,
            LaunchAtSignIn = false,
        };
        await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(legacy, JsonOptions));

        // 2. 写入旧版暂存 JSON
        var stagingStore = new StagingStore(Path.Combine(temp.Path, "Staging"));
        var oldMaterial = new StagedMaterial(
            Id: Guid.NewGuid(),
            Kind: StagedMaterialKind.Text,
            Title: "旧版暂存的文字",
            CreatedAt: DateTimeOffset.Parse("2026-07-20T10:00:00Z"),
            Status: StagedMaterialStatus.Deferred,
            Files: [],
            Content: "这是升级前暂存的内容");
        await stagingStore.SaveAsync(new StagingSnapshot([oldMaterial], string.Empty));

        // 3. 加载设置（触发迁移）
        var settingsStore = new SettingsStore(settingsPath);
        var loadResult = await settingsStore.LoadAsync();

        // 验证迁移成功
        Assert.True(loadResult.IsSuccess, $"Migration failed: {loadResult.ErrorMessage}");
        Assert.NotNull(loadResult.Settings);
        Assert.Equal(AppSettings.CurrentSchemaVersion, loadResult.Settings!.SchemaVersion);

        var profile = loadResult.Settings.CurrentProfile;
        Assert.NotNull(profile);
        Assert.Equal("E:/知识库/测试仓库", profile!.VaultPath);
        Assert.True(profile.CaptureTargets.Count >= 2);

        // 旧 Inbox 路径映射到目标（迁移生成 CreateNote 模式）
        var inboxTarget = profile.CaptureTargets.FirstOrDefault(t => t.Id == SettingsMigration.InboxTargetId);
        Assert.NotNull(inboxTarget);
        Assert.Equal("收件箱", inboxTarget!.Name);
        Assert.Equal(TargetWriteMode.CreateNote, inboxTarget.WriteMode);
        Assert.Contains("Inbox", inboxTarget.PathTemplate);

        // 旧 Daily 路径映射到目标
        var dailyTarget = profile.CaptureTargets.FirstOrDefault(t => t.Id == SettingsMigration.DailyTargetId);
        Assert.NotNull(dailyTarget);
        Assert.Equal(TargetWriteMode.AppendToPeriodicFile, dailyTarget!.WriteMode);

        // 4. 加载暂存材料（验证向后兼容）
        var loadedResult = await stagingStore.LoadAsync();
        Assert.Single(loadedResult.Snapshot.Items);
        var loadedMaterial = loadedResult.Snapshot.Items[0];
        Assert.Equal("旧版暂存的文字", loadedMaterial.Title);
        Assert.Equal("这是升级前暂存的内容", loadedMaterial.Content);
        Assert.Null(loadedMaterial.PreferredTargetId);

        // 5. 验证迁移幂等性（再次加载不重复迁移）
        var secondLoad = await settingsStore.LoadAsync();
        Assert.True(secondLoad.IsSuccess, $"Idempotent load failed: {secondLoad.ErrorMessage}");
        Assert.Equal(profile.CaptureTargets.Count, secondLoad.Settings!.CurrentProfile!.CaptureTargets.Count);
    }

    [Fact]
    public async Task Upgrade_WithChinesePaths_MigratesCorrectly()
    {
        using var temp = new TemporaryDirectory();
        var settingsPath = Path.Combine(temp.Path, "settings.json");

        var legacy = new LegacyAppSettings
        {
            VaultPath = "D:/我的知识库/中文仓库",
            InboxPath = "收件箱",
            DailyPath = "日常笔记",
            AttachmentsPath = "附件",
        };
        await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(legacy, JsonOptions));

        var settingsStore = new SettingsStore(settingsPath);
        var loadResult = await settingsStore.LoadAsync();

        Assert.True(loadResult.IsSuccess, $"Migration failed: {loadResult.ErrorMessage}");
        var profile = loadResult.Settings!.CurrentProfile!;
        Assert.Equal("D:/我的知识库/中文仓库", profile.VaultPath);

        var inboxTarget = profile.CaptureTargets.First(t => t.Id == SettingsMigration.InboxTargetId);
        Assert.Contains("收件箱", inboxTarget.PathTemplate);
    }

    [Fact]
    public async Task Upgrade_BackupCreated_BeforeMigration()
    {
        using var temp = new TemporaryDirectory();
        var settingsPath = Path.Combine(temp.Path, "settings.json");

        var legacy = new LegacyAppSettings
        {
            VaultPath = "E:/Test",
            InboxPath = "Inbox",
        };
        await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(legacy, JsonOptions));

        var settingsStore = new SettingsStore(settingsPath);
        var loadResult = await settingsStore.LoadAsync();
        Assert.True(loadResult.IsSuccess, $"Migration failed: {loadResult.ErrorMessage}");

        // 验证备份文件存在
        var backupPath = settingsPath + ".v1.bak";
        Assert.True(File.Exists(backupPath), "Backup file should exist after migration");

        // 备份内容包含原始路径信息
        var backupContent = await File.ReadAllTextAsync(backupPath);
        Assert.Contains("vaultPath", backupContent);
        Assert.Contains("Inbox", backupContent);
    }
}
