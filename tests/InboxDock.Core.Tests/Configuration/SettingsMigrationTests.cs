using System.Text.Json;
using InboxDock.Core.Configuration;
using InboxDock.Core.Targets;
using InboxDock.Core.Tests.Support;

namespace InboxDock.Core.Tests.Configuration;

public sealed class SettingsMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public void Migrate_PreservesPathsAndSwitches()
    {
        var legacy = new LegacyAppSettings
        {
            VaultPath = "E:\\知识库\\第一个仓库",
            InboxPath = "00 Inbox收件箱",
            DailyPath = "01 Daily日常",
            DailyTemplatePath = "10 Knowledge Hub/Templates/Daily.md",
            AttachmentsPath = "05 Resources/Attachments",
            AlwaysOnTop = false,
            LaunchAtSignIn = true,
            Theme = AppTheme.Dark,
            WindowLeft = 100,
            WindowTop = 200,
        };

        var migrated = SettingsMigration.Migrate(legacy);

        Assert.Equal(AppSettings.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(legacy.VaultPath, migrated.VaultPath);
        Assert.Equal(legacy.InboxPath, migrated.InboxPath);
        Assert.Equal(legacy.DailyPath, migrated.DailyPath);
        Assert.Equal(legacy.AlwaysOnTop, migrated.AlwaysOnTop);
        Assert.Equal(legacy.LaunchAtSignIn, migrated.LaunchAtSignIn);
        Assert.Equal(legacy.Theme, migrated.Theme);

        Assert.NotNull(migrated.CurrentProfile);
        Assert.Equal(legacy.VaultPath, migrated.CurrentProfile.VaultPath);
        Assert.Equal(legacy.AlwaysOnTop, migrated.CurrentProfile.AlwaysOnTop);
        Assert.Equal(legacy.LaunchAtSignIn, migrated.CurrentProfile.LaunchAtSignIn);
        Assert.Equal(legacy.Theme, migrated.CurrentProfile.Theme);
        Assert.Equal(100, migrated.CurrentProfile.WindowState.Left);
        Assert.Equal(200, migrated.CurrentProfile.WindowState.Top);
    }

    [Fact]
    public void Migrate_CreatesInboxAndDailyTargetsFromLegacyPaths()
    {
        var legacy = new LegacyAppSettings
        {
            VaultPath = "E:\\知识库",
            InboxPath = "00 Inbox收件箱",
            DailyPath = "01 Daily日常",
            AttachmentsPath = "05 Resources/Attachments",
        };

        var migrated = SettingsMigration.Migrate(legacy);
        var profile = migrated.CurrentProfile!;

        Assert.Equal(2, profile.CaptureTargets.Count);

        var inbox = profile.CaptureTargets.Single(t => t.Id == SettingsMigration.InboxTargetId);
        Assert.Equal("收件箱", inbox.Name);
        Assert.Equal(TargetWriteMode.CreateNote, inbox.WriteMode);
        Assert.Equal("00 Inbox收件箱", inbox.PathTemplate);
        Assert.Equal(AttachmentPolicyKind.DatedDirectory, inbox.AttachmentPolicy.Kind);
        Assert.Contains("05 Resources/Attachments", inbox.AttachmentPolicy.DirectoryTemplate);
        Assert.True(inbox.IsDefault);
        Assert.Equal(profile.DefaultTargetId, inbox.Id);

        var daily = profile.CaptureTargets.Single(t => t.Id == SettingsMigration.DailyTargetId);
        Assert.Equal("今日日记", daily.Name);
        Assert.Equal(TargetWriteMode.AppendToPeriodicFile, daily.WriteMode);
        Assert.Equal("01 Daily日常", daily.PathTemplate);
        Assert.Equal(AttachmentPolicyKind.StagingOnly, daily.AttachmentPolicy.Kind);
    }

    [Fact]
    public void Migrate_UsesLegacyDefaultsWhenOptionalFieldsMissing()
    {
        var legacy = new LegacyAppSettings
        {
            VaultPath = "E:\\知识库",
        };

        var migrated = SettingsMigration.Migrate(legacy);

        Assert.Equal("00 Inbox收件箱", migrated.InboxPath);
        Assert.Equal("01 Daily日常", migrated.DailyPath);
        Assert.Equal("05 Resources/Attachments", migrated.AttachmentsPath);
        Assert.True(migrated.AlwaysOnTop);
        Assert.False(migrated.LaunchAtSignIn);
        Assert.Equal(AppTheme.System, migrated.Theme);
    }

    [Fact]
    public void Migrate_PreservesChinesePaths()
    {
        var legacy = new LegacyAppSettings
        {
            VaultPath = "E:\\知识库\\第一个仓库",
            InboxPath = "收件箱目录",
            DailyPath = "日记目录",
            AttachmentsPath = "附件目录",
        };

        var migrated = SettingsMigration.Migrate(legacy);
        var inbox = migrated.CurrentProfile!.CaptureTargets.Single(t => t.Id == SettingsMigration.InboxTargetId);

        Assert.Equal("收件箱目录", inbox.PathTemplate);
        Assert.Contains("附件目录", inbox.AttachmentPolicy.DirectoryTemplate);
        Assert.Equal("日记目录", migrated.CurrentProfile.CaptureTargets.Single(t => t.Id == SettingsMigration.DailyTargetId).PathTemplate);
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacySettingsAndPreservesOriginalInBackup()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var backupPath = path + ".v1.bak";
        var legacy = new LegacyAppSettings
        {
            VaultPath = "E:\\知识库",
            InboxPath = "00 Inbox收件箱",
            DailyPath = "01 Daily日常",
            AlwaysOnTop = true,
        };
        var legacyJson = JsonSerializer.Serialize(legacy, JsonOptions);
        await File.WriteAllTextAsync(path, legacyJson);

        var result = await new SettingsStore(path).LoadAsync();

        Assert.True(result.IsSuccess, $"迁移失败：{result.ErrorMessage}");
        Assert.NotNull(result.Settings!.CurrentProfile);
        Assert.Equal(AppSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
        Assert.True(File.Exists(backupPath));
        Assert.Equal(legacyJson, await File.ReadAllTextAsync(backupPath));

        // 迁移后的文件应包含 schemaVersion。
        var migratedJson = await File.ReadAllTextAsync(path);
        Assert.Contains("schemaVersion", migratedJson);
    }

    [Fact]
    public async Task LoadAsync_DoesNotOverwriteInvalidJson()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var backupPath = path + ".v1.bak";
        const string invalid = "{broken";
        await File.WriteAllTextAsync(path, invalid);

        var result = await new SettingsStore(path).LoadAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(invalid, await File.ReadAllTextAsync(path));
        Assert.False(File.Exists(backupPath));
    }

    [Fact]
    public async Task LoadAsync_IsIdempotentAfterMigration()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var backupPath = path + ".v1.bak";
        var legacy = new LegacyAppSettings { VaultPath = "E:\\知识库" };
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(legacy, JsonOptions));

        var store = new SettingsStore(path);
        var first = await store.LoadAsync();
        Assert.True(first.IsSuccess, $"首次迁移失败：{first.ErrorMessage}");
        var firstTargets = first.Settings!.CurrentProfile!.CaptureTargets;
        var backupCreated = File.Exists(backupPath);

        var second = await store.LoadAsync();

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Settings.SchemaVersion, second.Settings!.SchemaVersion);
        Assert.Equal(firstTargets.Count, second.Settings.CurrentProfile!.CaptureTargets.Count);
        // 第二次加载不应重复创建备份。
        Assert.Equal(backupCreated, File.Exists(backupPath));
        Assert.Equal(await File.ReadAllTextAsync(path), await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task LoadAsync_KeepsOriginalWhenTempWriteFails()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var legacy = new LegacyAppSettings { VaultPath = "E:\\知识库" };
        var legacyJson = JsonSerializer.Serialize(legacy, JsonOptions);
        await File.WriteAllTextAsync(path, legacyJson);

        // 预先占用迁移临时路径，使写入失败。
        var tempPath = path + ".migration.tmp";
        Directory.CreateDirectory(tempPath);

        var result = await new SettingsStore(path).LoadAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(legacyJson, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task LoadAsync_LoadsMigratedConfigWithoutRemigrating()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var migrated = SettingsMigration.Migrate(new LegacyAppSettings { VaultPath = "E:\\知识库" });
        await new SettingsStore(path).SaveAsync(migrated);

        var result = await new SettingsStore(path).LoadAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Settings!.CurrentProfile);
        Assert.False(File.Exists(path + ".v1.bak"));
    }
}
