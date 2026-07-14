using InboxDock.Core.Capture;
using InboxDock.Core.Configuration;
using InboxDock.Core.Staging;
using InboxDock.Core.Vault;

namespace InboxDock.IntegrationTests;

public sealed class StagedCaptureServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "InboxDock.StagedCapture", Guid.NewGuid().ToString("N"));

    public StagedCaptureServiceTests()
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "vault", ".obsidian"));
    }

    [Fact]
    public async Task DeferAsync_ChangesOnlyStatusAndKeepsStagedFiles()
    {
        var (staging, capture) = CreateServices();
        await staging.LoadAsync();
        var source = await CreateSourceAsync("稍后.txt", "keep");
        var material = await staging.StageFilesAsync([source]);
        var stagedPath = Assert.Single(material.Files).StagedPath;

        var deferred = await capture.DeferAsync(material.Id);

        Assert.Equal(StagedMaterialStatus.Deferred, deferred.Status);
        Assert.Null(deferred.LastError);
        Assert.True(File.Exists(stagedPath));
        Assert.Single(staging.Snapshot.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmAsync_TextAndLink_CreateInboxNoteAndRemoveCard(bool link)
    {
        var (staging, capture) = CreateServices();
        await staging.LoadAsync();
        var material = link
            ? await staging.StagePastedLinkAsync("https://example.com/article")
            : await staging.StageDraftAsync("值得保留的想法");

        var result = await capture.ConfirmAsync(material!.Id);

        Assert.True(File.Exists(result.InboxNotePath));
        var markdown = await File.ReadAllTextAsync(result.InboxNotePath!);
        Assert.Contains(material.Content!, markdown);
        Assert.Empty(staging.Snapshot.Items);
    }

    [Fact]
    public async Task ConfirmAsync_FileGroup_CapturesFromStagingThenDeletesOnlyOwnedCopies()
    {
        var (staging, capture) = CreateServices();
        await staging.LoadAsync();
        var first = await CreateSourceAsync("原件一.txt", "one");
        var second = await CreateSourceAsync("原件二.txt", "two");
        var material = await staging.StageFilesAsync([first, second]);
        var stagedPaths = material.Files.Select(file => file.StagedPath).ToArray();

        var result = await capture.ConfirmAsync(material.Id);

        Assert.True(File.Exists(result.InboxNotePath));
        Assert.Equal(2, result.AttachmentPaths.Count);
        Assert.Equal("one", await File.ReadAllTextAsync(first));
        Assert.Equal("two", await File.ReadAllTextAsync(second));
        Assert.All(stagedPaths, path => Assert.False(File.Exists(path)));
        Assert.Empty(staging.Snapshot.Items);
    }

    [Fact]
    public async Task ConfirmAsync_WhenVaultCaptureFails_KeepsFailedCardAndStagedFile()
    {
        var store = new StagingStore(Path.Combine(root, "staging-failure"));
        var staging = new MaterialStagingService(store, new FileStagingService(store));
        await staging.LoadAsync();
        var source = await CreateSourceAsync("失败时保留.txt", "retry me");
        var material = await staging.StageFilesAsync([source]);
        var stagedPath = Assert.Single(material.Files).StagedPath;
        var blockedVault = Path.Combine(root, "blocked-vault");
        await File.WriteAllTextAsync(blockedVault, "not a directory");
        var inbox = new InboxCaptureService(new VaultLayout(AppSettings.CreateDefault(blockedVault)));
        var capture = new StagedCaptureService(staging, inbox);

        await Assert.ThrowsAnyAsync<IOException>(() => capture.ConfirmAsync(material.Id));

        var failed = Assert.Single(staging.Snapshot.Items);
        Assert.Equal(StagedMaterialStatus.Failed, failed.Status);
        Assert.False(string.IsNullOrWhiteSpace(failed.LastError));
        Assert.True(File.Exists(stagedPath));
        Assert.Equal("retry me", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task RemoveAsync_DeferredFileCard_DeletesOnlyInboxDockCopy()
    {
        var (staging, capture) = CreateServices();
        await staging.LoadAsync();
        var source = await CreateSourceAsync("不再需要.txt", "original");
        var material = await staging.StageFilesAsync([source]);
        var stagedPath = Assert.Single(material.Files).StagedPath;
        await capture.DeferAsync(material.Id);

        await capture.RemoveAsync(material.Id);

        Assert.Empty(staging.Snapshot.Items);
        Assert.False(File.Exists(stagedPath));
        Assert.Equal("original", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task RemoveAsync_CapturingCard_IsRejected()
    {
        var (staging, capture) = CreateServices();
        await staging.LoadAsync();
        var material = await staging.StageDraftAsync("处理中");
        await staging.UpdateAsync(material.Id, item => item with { Status = StagedMaterialStatus.Capturing });

        await Assert.ThrowsAsync<InvalidOperationException>(() => capture.RemoveAsync(material.Id));

        Assert.Single(staging.Snapshot.Items);
    }

    private (MaterialStagingService Staging, StagedCaptureService Capture) CreateServices()
    {
        var store = new StagingStore(Path.Combine(root, "staging"));
        var staging = new MaterialStagingService(store, new FileStagingService(store));
        var inbox = new InboxCaptureService(new VaultLayout(AppSettings.CreateDefault(Path.Combine(root, "vault"))));
        return (staging, new StagedCaptureService(staging, inbox));
    }

    private async Task<string> CreateSourceAsync(string name, string content)
    {
        var path = Path.Combine(root, "sources", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
