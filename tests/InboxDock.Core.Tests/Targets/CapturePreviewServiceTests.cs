using InboxDock.Core.Targets;
using InboxDock.Core.Templates;
using InboxDock.Core.Tests.Support;

namespace InboxDock.Core.Tests.Targets;

public sealed class CapturePreviewServiceTests
{
    private static readonly DateTimeOffset Clock =
        new(2026, 7, 23, 14, 30, 45, TimeSpan.FromHours(8));

    private static CapturePreviewService CreateService(TemporaryDirectory root) =>
        new(new TargetPathResolver(root.Path));

    private static TemplateContext Context(string? title = "测试") => new()
    {
        Title = title,
        Content = "正文内容",
        Now = Clock,
        Source = "clipboard",
    };

    [Fact]
    public void Preview_DoesNotModifyFileSystem()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
        };

        var before = Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories);

        var preview = service.Preview(target, Context());

        var after = Directory.GetFiles(root.Path, "*", SearchOption.AllDirectories);
        Assert.True(preview.IsValid);
        Assert.Empty(before);
        Assert.Empty(after);
    }

    [Fact]
    public void Preview_NewTarget_RequiresConfirmation()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            Revision = 1,
        };

        var preview = service.Preview(target, Context(), lastConfirmedRevision: null);

        Assert.True(preview.IsValid);
        Assert.True(preview.RequiresConfirmation);
        Assert.Contains("首次", preview.ConfirmationReason);
    }

    [Fact]
    public void Preview_ModifiedTarget_RequiresConfirmation()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            Revision = 3,
        };

        var preview = service.Preview(target, Context(), lastConfirmedRevision: 1);

        Assert.True(preview.IsValid);
        Assert.True(preview.RequiresConfirmation);
        Assert.Contains("修改", preview.ConfirmationReason);
    }

    [Fact]
    public void Preview_UnmodifiedTarget_AllowsQuickSave()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            Revision = 2,
        };

        var preview = service.Preview(target, Context(), lastConfirmedRevision: 2);

        Assert.True(preview.IsValid);
        Assert.False(preview.RequiresConfirmation);
    }

    [Fact]
    public void Preview_NameCollision_RequiresConfirmation()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(root.Path, "Inbox"));
        File.WriteAllText(Path.Combine(root.Path, "Inbox", "冲突.md"), "已有");
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            Revision = 1,
        };

        var preview = service.Preview(target, Context("冲突"), lastConfirmedRevision: 1);

        Assert.True(preview.IsValid);
        Assert.True(preview.RequiresConfirmation);
        Assert.Contains("同名", preview.ConfirmationReason);
    }

    [Fact]
    public void Preview_PathError_BlocksWrite()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "逃逸",
            WriteMode = TargetWriteMode.AppendToFile,
            PathTemplate = "../outside/note",
        };

        var preview = service.Preview(target, Context());

        Assert.False(preview.IsValid);
        Assert.NotNull(preview.UserErrorMessage);
        Assert.Null(preview.NotePath);
    }

    [Fact]
    public void Preview_TemplateError_BlocksWrite()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "模板错误",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{timestamp}}-{{title}}",
            ContentTemplate = "{{unknownVariable}}",
        };

        var preview = service.Preview(target, Context());

        Assert.False(preview.IsValid);
        Assert.NotNull(preview.UserErrorMessage);
    }

    [Fact]
    public void Preview_GeneratesMarkdownWithContentAndAttachments()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.DatedDirectory,
                DirectoryTemplate = "Attachments/{{date:yyyy-MM-dd}}",
            },
        };
        var attachments = new List<AttachmentInput>
        {
            new("截图.png", 1024),
        };

        var preview = service.Preview(target, Context("我的笔记"), attachments, lastConfirmedRevision: 1);

        Assert.True(preview.IsValid);
        Assert.Contains("# 我的笔记", preview.Markdown);
        Assert.Contains("## 内容", preview.Markdown);
        Assert.Contains("截图.png", preview.Markdown);
        Assert.Single(preview.AttachmentPaths);
        Assert.Contains("2026-07-23", preview.AttachmentPaths[0]);
    }

    [Fact]
    public void Preview_RendersCustomContentTemplate()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            ContentTemplate = "## {{title}}\n\n- 来源：{{source}}\n- 链接：{{url}}",
        };
        var context = new TemplateContext
        {
            Title = "标题",
            Content = "正文",
            Url = "https://example.com",
            Source = "drag",
            Now = Clock,
        };

        var preview = service.Preview(target, context, lastConfirmedRevision: 1);

        Assert.True(preview.IsValid);
        Assert.Contains("## 标题", preview.Markdown);
        Assert.Contains("来源：drag", preview.Markdown);
        Assert.Contains("https://example.com", preview.Markdown);
    }

    [Fact]
    public void Preview_StagingOnly_NeverRequiresConfirmation()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "只暂存",
            WriteMode = TargetWriteMode.StagingOnly,
            PathTemplate = string.Empty,
        };

        var preview = service.Preview(target, Context(), lastConfirmedRevision: null);

        Assert.True(preview.IsValid);
        Assert.False(preview.RequiresConfirmation);
        Assert.Null(preview.NotePath);
    }

    [Fact]
    public void Preview_SeparatesUserErrorsFromInternalDetails()
    {
        using var root = new TemporaryDirectory();
        var service = CreateService(root);
        var target = new CaptureTarget
        {
            Name = "错误",
            WriteMode = TargetWriteMode.AppendToFile,
            PathTemplate = "CON",
        };

        var preview = service.Preview(target, Context());

        Assert.False(preview.IsValid);
        Assert.NotNull(preview.UserErrorMessage);
        // 用户错误信息不包含堆栈或内部异常类型。
        Assert.DoesNotContain("System.", preview.UserErrorMessage!);
    }
}
