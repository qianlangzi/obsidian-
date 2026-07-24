using InboxDock.Core.Configuration;
using InboxDock.Core.Targets;
using InboxDock.Core.Tests.Support;

namespace InboxDock.Core.Tests.Configuration;

public sealed class SettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsUnicodeSettings()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var store = new SettingsStore(path);
        var expected = AppSettings.CreateDefault("E:\\知识库") with { Theme = AppTheme.Dark };

        await store.SaveAsync(expected);
        var result = await store.LoadAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Settings);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task SaveAndLoadAsync_PersistsSchemaVersionTwo()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var store = new SettingsStore(path);

        await store.SaveAsync(AppSettings.CreateDefault("E:\\知识库"));

        var result = await store.LoadAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(AppSettings.CurrentSchemaVersion, result.Settings!.SchemaVersion);
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsCurrentProfileWithTargets()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        var store = new SettingsStore(path);
        var targetId = Guid.NewGuid();
        var settings = AppSettings.CreateDefault("E:\\知识库") with
        {
            CurrentProfile = new VaultProfile
            {
                Name = "知识库",
                VaultPath = "E:\\知识库\\第一个仓库",
                DefaultTargetId = targetId,
                CaptureTargets =
                [
                    new CaptureTarget
                    {
                        Id = targetId,
                        Name = "今日日记",
                        WriteMode = TargetWriteMode.AppendToPeriodicFile,
                        PathTemplate = "01 Daily日常",
                    },
                ],
            },
        };

        await store.SaveAsync(settings);

        var result = await store.LoadAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Settings!.CurrentProfile);
        Assert.Equal("01 Daily日常", result.Settings.CurrentProfile.CaptureTargets[0].PathTemplate);
    }

    [Fact]
    public async Task LoadAsync_ReturnsRecoverableErrorForInvalidJson()
    {
        using var root = new TemporaryDirectory();
        var path = System.IO.Path.Combine(root.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{broken");

        var result = await new SettingsStore(path).LoadAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Settings);
        Assert.Contains("配置", result.ErrorMessage);
        Assert.Equal("{broken", await File.ReadAllTextAsync(path));
    }
}
