using System.Security.Cryptography;
using InboxDock.Core.Staging;

namespace InboxDock.IntegrationTests;

public sealed class MaterialStagingServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "InboxDock.Staging", Guid.NewGuid().ToString("N"));

    public MaterialStagingServiceTests() => Directory.CreateDirectory(root);

    [Fact]
    public async Task StageFilesAsync_CopiesOneFileWithoutChangingSource()
    {
        var source = await CreateFileAsync("来源.txt", "original");
        var before = await HashAsync(source);
        var service = CreateService();
        await service.LoadAsync();

        var material = await service.StageFilesAsync([source]);

        Assert.Equal(StagedMaterialKind.Files, material.Kind);
        Assert.Equal(StagedMaterialStatus.AwaitingConfirmation, material.Status);
        var file = Assert.Single(material.Files);
        Assert.True(File.Exists(file.StagedPath));
        Assert.NotEqual(source, file.StagedPath);
        Assert.Equal(before, await HashAsync(source));
        Assert.Equal("original", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task StageFilesAsync_GroupsFilesAndPreservesChineseNames()
    {
        var first = await CreateFileAsync(Path.Combine("甲", "报告.pdf"), "one");
        var second = await CreateFileAsync(Path.Combine("乙", "截图.png"), "two");
        var service = CreateService();
        await service.LoadAsync();

        var material = await service.StageFilesAsync([first, second]);

        Assert.Equal(2, material.Files.Count);
        Assert.Equal(["报告.pdf", "截图.png"], material.Files.Select(file => file.OriginalName));
        Assert.All(material.Files, file => Assert.Contains(material.Id.ToString("N"), file.StagedPath));
        Assert.Single(service.Snapshot.Items);
    }

    [Fact]
    public async Task StageFilesAsync_DuplicateNamesReceiveUniqueStagedNames()
    {
        var first = await CreateFileAsync(Path.Combine("甲", "资料.txt"), "one");
        var second = await CreateFileAsync(Path.Combine("乙", "资料.txt"), "two");
        var service = CreateService();
        await service.LoadAsync();

        var material = await service.StageFilesAsync([first, second]);

        Assert.Equal(["资料.txt", "资料 (2).txt"], material.Files.Select(file => Path.GetFileName(file.StagedPath)));
        Assert.Equal("one", await File.ReadAllTextAsync(material.Files[0].StagedPath));
        Assert.Equal("two", await File.ReadAllTextAsync(material.Files[1].StagedPath));
    }

    [Fact]
    public async Task StageFilesAsync_RejectsDirectories()
    {
        var directory = Path.Combine(root, "folder");
        Directory.CreateDirectory(directory);
        var service = CreateService();
        await service.LoadAsync();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.StageFilesAsync([directory]));

        Assert.Contains("文件夹", error.Message);
        Assert.Empty(service.Snapshot.Items);
    }

    [Fact]
    public async Task StageFilesAsync_WhenOneSourceIsMissing_RollsBackWholeGroup()
    {
        var source = await CreateFileAsync("存在.txt", "keep");
        var missing = Path.Combine(root, "不存在.txt");
        var service = CreateService();
        await service.LoadAsync();

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.StageFilesAsync([source, missing]));

        Assert.Empty(service.Snapshot.Items);
        var filesRoot = Path.Combine(root, "staging", "files");
        Assert.False(Directory.Exists(filesRoot) && Directory.EnumerateFileSystemEntries(filesRoot).Any());
        Assert.Equal("keep", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task StageFilesAsync_AfterRestart_RestoresPersistedGroup()
    {
        var source = await CreateFileAsync("重启.txt", "persist");
        var first = CreateService();
        await first.LoadAsync();
        var staged = await first.StageFilesAsync([source]);

        var restarted = CreateService();
        var result = await restarted.LoadAsync();

        Assert.Null(result.Error);
        var restored = Assert.Single(restarted.Snapshot.Items);
        Assert.Equal(staged.Id, restored.Id);
        Assert.True(File.Exists(Assert.Single(restored.Files).StagedPath));
    }

    [Fact]
    public async Task StageFilesAsync_SequentialGroups_UpdateCurrentSnapshot()
    {
        var first = await CreateFileAsync("first.txt", "one");
        var second = await CreateFileAsync("second.txt", "two");
        var service = CreateService();
        await service.LoadAsync();

        var firstMaterial = await service.StageFilesAsync([first]);
        var secondMaterial = await service.StageFilesAsync([second]);

        Assert.Equal(2, service.Snapshot.Items.Count);
        Assert.Contains(service.Snapshot.Items, item => item.Id == firstMaterial.Id);
        Assert.Contains(service.Snapshot.Items, item => item.Id == secondMaterial.Id);
    }

    [Theory]
    [InlineData("https://example.com/path", true)]
    [InlineData("http://example.com", true)]
    [InlineData("ftp://example.com", false)]
    [InlineData("example.com", false)]
    [InlineData("普通文字", false)]
    public void TryNormalizeHttpUrl_AcceptsOnlyAbsoluteHttpAndHttps(string input, bool expected)
    {
        var recognized = MaterialStagingService.TryNormalizeHttpUrl(input, out var normalized);

        Assert.Equal(expected, recognized);
        Assert.Equal(expected ? input : string.Empty, normalized);
    }

    [Fact]
    public async Task StageDraftAsync_CreatesTextCardAndClearsPersistedDraft()
    {
        var service = CreateService();
        await service.LoadAsync();
        await service.SaveDraftAsync("未完成草稿");

        var material = await service.StageDraftAsync("一段文字");
        var restarted = CreateService();
        await restarted.LoadAsync();

        Assert.Equal(StagedMaterialKind.Text, material.Kind);
        Assert.Equal("一段文字", material.Content);
        Assert.Equal(string.Empty, restarted.Snapshot.DraftText);
    }

    private MaterialStagingService CreateService()
    {
        var store = new StagingStore(Path.Combine(root, "staging"));
        return new MaterialStagingService(store, new FileStagingService(store));
    }

    private async Task<string> CreateFileAsync(string relativePath, string content)
    {
        var path = Path.Combine(root, "sources", relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static async Task<string> HashAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
