using InboxDock.Core.Capture;
using InboxDock.Core.Configuration;
using InboxDock.Core.Daily;
using InboxDock.Core.History;
using InboxDock.Core.Vault;

namespace InboxDock.IntegrationTests;

public sealed class CaptureWorkflowTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "InboxDock.Integration", Guid.NewGuid().ToString("N"));

    public CaptureWorkflowTests()
    {
        Directory.CreateDirectory(Path.Combine(root, ".obsidian"));
    }

    [Fact]
    public async Task TextAndFileCapture_CreateInboxRecordsWithoutChangingSources()
    {
        var layout = new VaultLayout(AppSettings.CreateDefault(root));
        var service = new InboxCaptureService(layout);
        var text = await service.CaptureTextAsync("理解了接口");
        var source = Path.Combine(root, "原始报告.pdf");
        await File.WriteAllTextAsync(source, "original");
        var files = await service.CaptureFilesAsync([source]);

        Assert.True(File.Exists(text.InboxNotePath));
        Assert.True(File.Exists(files.InboxNotePath));
        Assert.Equal("original", await File.ReadAllTextAsync(source));
        Assert.Single(files.AttachmentPaths);
    }

    [Fact]
    public async Task DailyAppendAndUndo_TouchOnlyMarkedRecord()
    {
        var layout = new VaultLayout(AppSettings.CreateDefault(root));
        var daily = new DailyCaptureService(layout);
        var result = await daily.AppendAsync(DailyCategory.Learning, "理解了接口");
        await File.AppendAllTextAsync(result.DailyNotePath!, "\n用户内容\n");

        var undone = await new UndoService().UndoAsync(result);
        var content = await File.ReadAllTextAsync(result.DailyNotePath!);

        Assert.True(undone.IsSuccess);
        Assert.DoesNotContain("理解了接口", content);
        Assert.Contains("用户内容", content);
    }

    [Fact]
    public async Task InboxUndo_MovesCreatedFilesToRecovery()
    {
        var layout = new VaultLayout(AppSettings.CreateDefault(root));
        var source = Path.Combine(root, "原始.txt");
        await File.WriteAllTextAsync(source, "keep me");
        var capture = await new InboxCaptureService(layout).CaptureFilesAsync([source]);
        var recovery = Path.Combine(root, "recovery");

        var result = await new UndoService(recovery).UndoAsync(capture);

        Assert.True(result.IsSuccess);
        Assert.Equal("keep me", await File.ReadAllTextAsync(source));
        Assert.False(File.Exists(capture.InboxNotePath));
        Assert.All(capture.AttachmentPaths, path => Assert.False(File.Exists(path)));
        Assert.NotEmpty(Directory.GetFiles(recovery, "*", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
