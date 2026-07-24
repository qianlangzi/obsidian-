using InboxDock.Core.Capture;
using InboxDock.Core.Configuration;
using InboxDock.Core.Staging;
using InboxDock.Core.Targets;
using InboxDock.Core.Vault;

namespace InboxDock.IntegrationTests;

public sealed class StagedCaptureServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "InboxDock.StagedCapture", Guid.NewGuid().ToString("N"));
    private readonly string vaultRoot;

    public StagedCaptureServiceTests()
    {
        Directory.CreateDirectory(root);
        vaultRoot = Path.Combine(root, "vault");
        Directory.CreateDirectory(Path.Combine(vaultRoot, ".obsidian"));
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
        material = await staging.UpdateAsync(
            material.Id,
            item => item with { Note = "一起阅读并比较" });
        var stagedPaths = material.Files.Select(file => file.StagedPath).ToArray();

        var result = await capture.ConfirmAsync(material.Id);

        Assert.True(File.Exists(result.InboxNotePath));
        Assert.Contains("一起阅读并比较", await File.ReadAllTextAsync(result.InboxNotePath!));
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

    [Fact]
    public async Task ConfirmToTargetAsync_Text_WritesToVaultAndRemovesCard()
    {
        var (staging, capture) = CreateTargetServices();
        await staging.LoadAsync();
        var material = await staging.StageDraftAsync("通过目标写入");

        var result = await capture.ConfirmToTargetAsync(material.Id, MakeAppendTarget(), vaultRoot);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(result.NotePath));
        Assert.Contains("通过目标写入", await File.ReadAllTextAsync(result.NotePath!));
        Assert.Empty(staging.Snapshot.Items);
    }

    [Fact]
    public async Task ConfirmToTargetAsync_WhenVaultMissing_KeepsCardWithError()
    {
        var (staging, _) = CreateTargetServices();
        await staging.LoadAsync();
        var material = await staging.StageDraftAsync("会失败");
        var capture = CreateTargetServicesFor(staging, "/missing/vault");

        var result = await capture.ConfirmToTargetAsync(material.Id, MakeAppendTarget(), "/missing/vault");

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        var failed = Assert.Single(staging.Snapshot.Items);
        Assert.Equal(StagedMaterialStatus.Failed, failed.Status);
        Assert.False(string.IsNullOrWhiteSpace(failed.LastError));
    }

    [Fact]
    public async Task ConfirmToTargetAsync_RetryAfterFailure_ClearsErrorOnSuccess()
    {
        var (staging, capture) = CreateTargetServices();
        await staging.LoadAsync();
        var material = await staging.StageDraftAsync("先失败后成功");
        var badCapture = CreateTargetServicesFor(staging, "/missing/vault");
        await badCapture.ConfirmToTargetAsync(material.Id, MakeAppendTarget(), "/missing/vault");
        var failed = Assert.Single(staging.Snapshot.Items);
        Assert.Equal(StagedMaterialStatus.Failed, failed.Status);

        var result = await capture.ConfirmToTargetAsync(material.Id, MakeAppendTarget(), vaultRoot);

        Assert.True(result.IsSuccess);
        Assert.Empty(staging.Snapshot.Items);
    }

    [Fact]
    public async Task ConfirmToTargetAsync_KeepStaged_KeepsCardAfterSuccess()
    {
        var (staging, capture) = CreateTargetServices();
        await staging.LoadAsync();
        var material = await staging.StageDraftAsync("保留卡片");
        var target = MakeAppendTarget() with { PostCaptureBehavior = PostCaptureBehavior.KeepStaged };

        var result = await capture.ConfirmToTargetAsync(material.Id, target, vaultRoot);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(result.NotePath));
        var kept = Assert.Single(staging.Snapshot.Items);
        Assert.Equal(StagedMaterialStatus.AwaitingConfirmation, kept.Status);
        Assert.Null(kept.LastError);
    }

    [Fact]
    public async Task ConfirmBatchAsync_PartialFailure_KeepsFailedCardAndContinuesOthers()
    {
        var (staging, capture) = CreateTargetServices();
        await staging.LoadAsync();
        var goodText = await staging.StageDraftAsync("批量成功项");
        var source = await CreateSourceAsync("批量失败.txt", "内容");
        var doomedFiles = await staging.StageFilesAsync([source]);
        // 删除暂存文件，制造写入失败（预检附件源文件时发现不存在）。
        File.Delete(doomedFiles.Files[0].StagedPath);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Notes",
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FixedDirectory,
                DirectoryTemplate = "Attachments",
            },
        };

        var batch = await capture.ConfirmBatchAsync([goodText.Id, doomedFiles.Id], target, vaultRoot);

        Assert.Equal(1, batch.SuccessCount);
        Assert.Equal(1, batch.FailureCount);
        var remaining = Assert.Single(staging.Snapshot.Items);
        Assert.Equal(doomedFiles.Id, remaining.Id);
        Assert.Equal(StagedMaterialStatus.Failed, remaining.Status);
        Assert.False(string.IsNullOrWhiteSpace(remaining.LastError));
    }

    private (MaterialStagingService Staging, StagedCaptureService Capture) CreateServices()
    {
        var store = new StagingStore(Path.Combine(root, "staging"));
        var staging = new MaterialStagingService(store, new FileStagingService(store));
        var inbox = new InboxCaptureService(new VaultLayout(AppSettings.CreateDefault(Path.Combine(root, "vault"))));
        return (staging, new StagedCaptureService(staging, inbox));
    }

    private (MaterialStagingService Staging, StagedCaptureService Capture) CreateTargetServices()
    {
        var store = new StagingStore(Path.Combine(root, "staging-target"));
        var staging = new MaterialStagingService(store, new FileStagingService(store));
        return (staging, CreateTargetServicesFor(staging, vaultRoot));
    }

    private StagedCaptureService CreateTargetServicesFor(MaterialStagingService staging, string vaultRoot)
    {
        var inbox = new InboxCaptureService(new VaultLayout(AppSettings.CreateDefault(Path.Combine(root, "vault"))));
        var previewService = new CapturePreviewService(new TargetPathResolver(vaultRoot));
        var writeService = new TargetWriteService(Path.Combine(root, "recovery"));
        return new StagedCaptureService(staging, inbox, previewService, writeService);
    }

    private static CaptureTarget MakeAppendTarget() => new()
    {
        Name = "收件箱",
        WriteMode = TargetWriteMode.AppendToFile,
        PathTemplate = "Inbox/收件箱.md",
        AttachmentPolicy = AttachmentPolicy.StagingOnly,
    };

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
