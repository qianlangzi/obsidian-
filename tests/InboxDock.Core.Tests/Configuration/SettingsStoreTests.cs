using InboxDock.Core.Configuration;
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
