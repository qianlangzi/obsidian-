using InboxDock.Core.History;
using InboxDock.Core.IO;
using InboxDock.Core.Targets;
using InboxDock.Core.Templates;

namespace InboxDock.IntegrationTests;

public sealed class TargetWriteServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "InboxDock.TargetWrite", Guid.NewGuid().ToString("N"));
    private readonly string vaultRoot;
    private readonly string recoveryRoot;

    public TargetWriteServiceTests()
    {
        vaultRoot = Path.Combine(root, "vault");
        Directory.CreateDirectory(vaultRoot);
        Directory.CreateDirectory(Path.Combine(vaultRoot, ".obsidian"));
        recoveryRoot = Path.Combine(root, "recovery");
    }

    [Fact]
    public async Task AppendToFile_PreservesExistingUserContentAndUsesMarkers()
    {
        var target = MakeTarget(TargetWriteMode.AppendToFile, pathTemplate: "Inbox/收件箱.md");
        var notePath = Path.Combine(vaultRoot, "Inbox", "收件箱.md");
        Directory.CreateDirectory(Path.GetDirectoryName(notePath)!);
        await File.WriteAllTextAsync(notePath, "已有用户内容\n");

        var result = await WriteTextAsync(target, "新的快速记录");

        Assert.True(result.IsSuccess);
        Assert.Equal(notePath, result.NotePath);
        var content = await File.ReadAllTextAsync(notePath);
        Assert.Contains("已有用户内容", content);
        Assert.Contains("新的快速记录", content);
        Assert.Contains(WriteMarker.Start(result.CaptureId), content);
        Assert.Contains(WriteMarker.End(result.CaptureId), content);
    }

    [Fact]
    public async Task AppendToPeriodicFile_RendersDailyFileUnderDirectory()
    {
        var now = new DateTimeOffset(2026, 7, 23, 10, 15, 0, TimeSpan.FromHours(8));
        var target = MakeTarget(
            TargetWriteMode.AppendToPeriodicFile,
            pathTemplate: "Daily",
            fileNameTemplate: "{{date:yyyy-MM-dd}}.md");

        var result = await WriteTextAsync(target, "今日学习", now: now);

        Assert.True(result.IsSuccess);
        var expected = Path.Combine(vaultRoot, "Daily", "2026-07-23.md");
        Assert.Equal(expected, result.NotePath);
        Assert.True(File.Exists(expected));
        var content = await File.ReadAllTextAsync(expected);
        Assert.Contains("今日学习", content);
    }

    [Fact]
    public async Task CreateNote_WritesNewMarkdownFileWithFootprint()
    {
        var target = MakeTarget(
            TargetWriteMode.CreateNote,
            pathTemplate: "Notes",
            fileNameTemplate: "{{timestamp}}-{{title}}");

        var result = await WriteTextAsync(target, "一条想法", title: "想法");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.NotePath);
        Assert.True(File.Exists(result.NotePath));
        Assert.EndsWith(".md", result.NotePath);
        var content = await File.ReadAllTextAsync(result.NotePath);
        Assert.Contains("一条想法", content);
        Assert.Contains(WriteMarker.Footprint(result.CaptureId), content);
    }

    [Fact]
    public async Task StagingOnly_CreatesNoVaultFiles()
    {
        var target = MakeTarget(TargetWriteMode.StagingOnly);

        var result = await WriteTextAsync(target, "只暂存内容");

        Assert.True(result.IsSuccess);
        Assert.Equal(TargetWriteMode.StagingOnly, result.WriteMode);
        Assert.Null(result.NotePath);
        Assert.Empty(result.AttachmentPaths);
        var vaultFiles = Directory.GetFiles(vaultRoot, "*", SearchOption.AllDirectories);
        Assert.DoesNotContain(vaultFiles, path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WriteFiles_OriginalSourcesAreNeverMovedOrDeleted()
    {
        var target = MakeTarget(
            TargetWriteMode.CreateNote,
            pathTemplate: "Notes",
            policy: new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FixedDirectory,
                DirectoryTemplate = "Attachments",
            });
        var first = await CreateSourceFileAsync("原件.txt", "原始内容一");
        var second = await CreateSourceFileAsync("副本.txt", "原始内容二");

        var preview = await PreviewFilesAsync(target, [first, second]);
        var result = await WriteAsync(target, preview, [first, second]);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.AttachmentPaths.Count);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.Equal("原始内容一", await File.ReadAllTextAsync(first));
        Assert.Equal("原始内容二", await File.ReadAllTextAsync(second));
    }

    [Fact]
    public async Task WriteFiles_DoesNotOverwriteExistingAttachmentWithSameName()
    {
        var target = MakeTarget(
            TargetWriteMode.CreateNote,
            pathTemplate: "Notes",
            policy: new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FixedDirectory,
                DirectoryTemplate = "Attachments",
            });
        var existingAttachment = Path.Combine(vaultRoot, "Attachments", "同名.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(existingAttachment)!);
        await File.WriteAllTextAsync(existingAttachment, "原有附件不应被覆盖");
        var source = await CreateSourceFileAsync("同名.txt", "新内容");

        var preview = await PreviewFilesAsync(target, [source]);
        var result = await WriteAsync(target, preview, [source]);

        Assert.True(result.IsSuccess);
        Assert.Equal("原有附件不应被覆盖", await File.ReadAllTextAsync(existingAttachment));
        var writtenAttachment = Assert.Single(result.AttachmentPaths, path => path != existingAttachment);
        Assert.Contains("同名", Path.GetFileNameWithoutExtension(writtenAttachment));
        Assert.Equal("新内容", await File.ReadAllTextAsync(writtenAttachment));
    }

    [Fact]
    public async Task WriteFiles_WhenAttachmentDirectoryBlocked_RollsBackAndReportsSafeStorage()
    {
        var target = MakeTarget(
            TargetWriteMode.CreateNote,
            pathTemplate: "Notes",
            policy: new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FixedDirectory,
                DirectoryTemplate = "Attachments",
            });
        var source = await CreateSourceFileAsync("附件.txt", "附件内容");
        // 在附件目录位置创建一个普通文件，阻止目录创建，触发写入异常走回滚分支。
        await File.WriteAllTextAsync(Path.Combine(vaultRoot, "Attachments"), "blocker");

        var preview = await PreviewFilesAsync(target, [source]);
        var result = await WriteAsync(target, preview, [source]);

        Assert.False(result.IsSuccess);
        Assert.Contains("材料仍安全保存", result.ErrorMessage);
        var notesDir = Path.Combine(vaultRoot, "Notes");
        Assert.True(!Directory.Exists(notesDir) || Directory.GetFiles(notesDir, "*.md").Length == 0);
        Assert.True(File.Exists(source));
        Assert.Equal("附件内容", await File.ReadAllTextAsync(source));
    }

    [Fact]
    public async Task UndoAppend_RemovesOnlyMarkedBlockAndKeepsUserContent()
    {
        var target = MakeTarget(TargetWriteMode.AppendToFile, pathTemplate: "Inbox/收件箱.md");
        var notePath = Path.Combine(vaultRoot, "Inbox", "收件箱.md");
        Directory.CreateDirectory(Path.GetDirectoryName(notePath)!);
        await File.WriteAllTextAsync(notePath, "用户原始段\n");

        var result = await WriteTextAsync(target, "本次写入内容");
        await File.AppendAllTextAsync(notePath, "\n用户在写入后追加的内容\n");

        var undone = await new UndoService(recoveryRoot).UndoWriteAsync(result);

        Assert.True(undone.IsSuccess);
        var content = await File.ReadAllTextAsync(notePath);
        Assert.Contains("用户原始段", content);
        Assert.Contains("用户在写入后追加的内容", content);
        Assert.DoesNotContain("本次写入内容", content);
        Assert.DoesNotContain(WriteMarker.Start(result.CaptureId), content);
    }

    [Fact]
    public async Task UndoCreateNote_MovesNoteAndAttachmentsToRecovery()
    {
        var target = MakeTarget(
            TargetWriteMode.CreateNote,
            pathTemplate: "Notes",
            policy: new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FixedDirectory,
                DirectoryTemplate = "Attachments",
            });
        var source = await CreateSourceFileAsync("附件.txt", "附件内容");

        var preview = await PreviewFilesAsync(target, [source]);
        var result = await WriteAsync(target, preview, [source]);
        var notePath = result.NotePath!;
        var attachmentPath = Assert.Single(result.AttachmentPaths);

        var undone = await new UndoService(recoveryRoot).UndoWriteAsync(result);

        Assert.True(undone.IsSuccess);
        Assert.False(File.Exists(notePath));
        Assert.False(File.Exists(attachmentPath));
        var recoveredFiles = Directory.GetFiles(recoveryRoot, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(recoveredFiles);
        var recoveredAttachment = recoveredFiles.Single(path => path.EndsWith("附件.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("附件内容", await File.ReadAllTextAsync(recoveredAttachment));
    }

    [Fact]
    public async Task UndoAppend_WhenMarkersAreModified_RejectsDangerousUndo()
    {
        var target = MakeTarget(TargetWriteMode.AppendToFile, pathTemplate: "Inbox/收件箱.md");
        var notePath = Path.Combine(vaultRoot, "Inbox", "收件箱.md");

        var result = await WriteTextAsync(target, "原始写入");
        var content = await File.ReadAllTextAsync(notePath);
        var tampered = content.Replace(WriteMarker.Start(result.CaptureId), "<!-- 已被用户修改 -->");
        await AtomicFile.ReplaceTextAsync(notePath, tampered);

        var undone = await new UndoService(recoveryRoot).UndoWriteAsync(result);

        Assert.False(undone.IsSuccess);
        Assert.Contains("拒绝", undone.Message);
        var current = await File.ReadAllTextAsync(notePath);
        Assert.Contains("原始写入", current);
    }

    [Fact]
    public async Task UndoStagingOnly_ReturnsNoopSuccess()
    {
        var target = MakeTarget(TargetWriteMode.StagingOnly);
        var result = await WriteTextAsync(target, "只暂存");

        var undone = await new UndoService(recoveryRoot).UndoWriteAsync(result);

        Assert.True(undone.IsSuccess);
    }

    private Task<TargetWriteResult> WriteTextAsync(
        CaptureTarget target,
        string content,
        string? title = null,
        DateTimeOffset? now = null)
    {
        var context = new TemplateContext
        {
            Content = content,
            Title = title,
            Now = now ?? DateTimeOffset.Now,
            Source = "test",
        };
        var preview = new CapturePreviewService(new TargetPathResolver(vaultRoot)).Preview(target, context);
        var request = new TargetWriteRequest { Target = target, Preview = preview };
        return new TargetWriteService(recoveryRoot).WriteAsync(request, vaultRoot);
    }

    private Task<CapturePreview> PreviewFilesAsync(CaptureTarget target, IReadOnlyList<string> sources)
    {
        var attachments = sources
            .Select(path => new AttachmentInput(Path.GetFileName(path), new FileInfo(path).Length))
            .ToArray();
        var context = new TemplateContext
        {
            Now = DateTimeOffset.Now,
            Source = "test",
        };
        var preview = new CapturePreviewService(new TargetPathResolver(vaultRoot)).Preview(target, context, attachments);
        return Task.FromResult(preview);
    }

    private Task<TargetWriteResult> WriteAsync(
        CaptureTarget target,
        CapturePreview preview,
        IReadOnlyList<string> sources)
    {
        var request = new TargetWriteRequest { Target = target, Preview = preview, SourceFiles = sources };
        return new TargetWriteService(recoveryRoot).WriteAsync(request, vaultRoot);
    }

    private static CaptureTarget MakeTarget(
        TargetWriteMode mode,
        string pathTemplate = "",
        string fileNameTemplate = "",
        AttachmentPolicy? policy = null) => new()
        {
            Name = mode.ToString(),
            WriteMode = mode,
            PathTemplate = pathTemplate,
            FileNameTemplate = fileNameTemplate,
            AttachmentPolicy = policy ?? AttachmentPolicy.StagingOnly,
        };

    private async Task<string> CreateSourceFileAsync(string name, string content)
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
