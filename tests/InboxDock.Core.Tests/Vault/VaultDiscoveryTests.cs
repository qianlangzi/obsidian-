using System.Text;
using InboxDock.Core.Tests.Support;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Tests.Vault;

public sealed class VaultDiscoveryTests
{
    [Fact]
    public async Task DiscoverAsync_VaultMissingDotObsidian_ReturnsInvalid()
    {
        using var root = new TemporaryDirectory();

        var result = await new VaultDiscovery().DiscoverAsync(root.Path);

        Assert.False(result.IsValid);
        Assert.Contains(".obsidian", result.Message);
    }

    [Fact]
    public async Task DiscoverAsync_EmptyVault_ReturnsValidWithNoSuggestions()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, ".obsidian"));

        var result = await new VaultDiscovery().DiscoverAsync(root.Path);

        Assert.True(result.IsValid);
        Assert.Null(result.AttachmentFolder);
        Assert.Null(result.DailyNotesFolder);
    }

    [Fact]
    public async Task DiscoverAsync_ReadsAttachmentFolderFromAppJson()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, ".obsidian"));
        var appJson = """
            {
              "newFileLocation": 1,
              "attachmentFolderPath": "05 Resources/Attachments"
            }
            """;
        await File.WriteAllTextAsync(System.IO.Path.Combine(root.Path, ".obsidian", "app.json"), appJson);

        var result = await new VaultDiscovery().DiscoverAsync(root.Path);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.AttachmentLocationMode);
        Assert.Equal("05 Resources/Attachments", result.AttachmentFolder);
    }

    [Fact]
    public async Task DiscoverAsync_ReadsDailyNotesConfiguration()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, ".obsidian"));
        var dailyJson = """
            {
              "folder": "01 Daily日常",
              "format": "YYYY-MM-DD",
              "template": "10 Templates/Daily.md"
            }
            """;
        await File.WriteAllTextAsync(System.IO.Path.Combine(root.Path, ".obsidian", "daily-notes.json"), dailyJson);

        var result = await new VaultDiscovery().DiscoverAsync(root.Path);

        Assert.True(result.IsValid);
        Assert.Equal("01 Daily日常", result.DailyNotesFolder);
        Assert.Equal("YYYY-MM-DD", result.DailyNotesFormat);
        Assert.Equal("10 Templates/Daily.md", result.DailyNotesTemplate);
    }

    [Fact]
    public async Task DiscoverAsync_CorruptedJson_DoesNotThrowAndContinuesWithOthers()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, ".obsidian"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(root.Path, ".obsidian", "app.json"), "{ broken");
        var dailyJson = """
            {
              "folder": "Daily",
              "format": "YYYY-MM-DD"
            }
            """;
        await File.WriteAllTextAsync(System.IO.Path.Combine(root.Path, ".obsidian", "daily-notes.json"), dailyJson);

        var result = await new VaultDiscovery().DiscoverAsync(root.Path);

        Assert.True(result.IsValid);
        Assert.Null(result.AttachmentFolder);
        Assert.Equal("Daily", result.DailyNotesFolder);
    }

    [Fact]
    public async Task DiscoverAsync_DoesNotModifyConfigurationFiles()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, ".obsidian"));
        var appJsonPath = System.IO.Path.Combine(root.Path, ".obsidian", "app.json");
        var dailyPath = System.IO.Path.Combine(root.Path, ".obsidian", "daily-notes.json");
        var appContent = """
            {
              "newFileLocation": 1,
              "attachmentFolderPath": "attachments"
            }
            """;
        var dailyContent = """
            {
              "folder": "daily",
              "format": "YYYY-MM-DD"
            }
            """;
        await File.WriteAllTextAsync(appJsonPath, appContent);
        await File.WriteAllTextAsync(dailyPath, dailyContent);
        var before = (File.GetLastWriteTimeUtc(appJsonPath), File.GetLastWriteTimeUtc(dailyPath));

        await new VaultDiscovery().DiscoverAsync(root.Path);

        var after = (File.GetLastWriteTimeUtc(appJsonPath), File.GetLastWriteTimeUtc(dailyPath));
        Assert.Equal(before, after);
        Assert.Equal(appContent, await File.ReadAllTextAsync(appJsonPath));
        Assert.Equal(dailyContent, await File.ReadAllTextAsync(dailyPath));
    }

    [Fact]
    public async Task DiscoverAsync_EmptyOptionalFields_ReturnsNullWithoutError()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, ".obsidian"));
        var appJson = """
            {
              "newFileLocation": 0,
              "attachmentFolderPath": ""
            }
            """;
        await File.WriteAllTextAsync(System.IO.Path.Combine(root.Path, ".obsidian", "app.json"), appJson);

        var result = await new VaultDiscovery().DiscoverAsync(root.Path);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.AttachmentLocationMode);
        Assert.Null(result.AttachmentFolder);
    }
}
