using InboxDock.Core.Capture;
using InboxDock.Core.Targets;
using InboxDock.Core.Templates;
using InboxDock.Core.Tests.Support;

namespace InboxDock.Core.Tests.Targets;

public sealed class TargetPathResolverTests
{
    private static readonly DateTimeOffset Clock =
        new(2026, 7, 23, 14, 30, 45, TimeSpan.FromHours(8));

    private static TargetPathResolver CreateResolver(TemporaryDirectory root) => new(root.Path);

    private static TemplateContext Context(string? title = "测试", string? content = "内容") => new()
    {
        Title = title,
        Content = content,
        Now = Clock,
    };

    [Fact]
    public void CreateNote_ResolvesDirectoryAndFileName()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "00 Inbox收件箱",
            FileNameTemplate = "{{timestamp}}-{{title}}",
        };

        var result = resolver.Resolve(target, Context("我的笔记"));

        Assert.True(result.IsValid, result.Message);
        Assert.EndsWith("00 Inbox收件箱\\2026-07-23-143045-我的笔记.md", result.ResolvedPaths!.NotePath);
        Assert.Equal("00 Inbox收件箱/2026-07-23-143045-我的笔记.md", result.ResolvedPaths.RelativeNotePath);
    }

    [Fact]
    public void AppendToFile_ResolvesMarkdownFile()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "日记",
            WriteMode = TargetWriteMode.AppendToFile,
            PathTemplate = "Logs/inbox",
        };

        var result = resolver.Resolve(target, Context());

        Assert.True(result.IsValid, result.Message);
        Assert.EndsWith("Logs\\inbox.md", result.ResolvedPaths!.NotePath);
    }

    [Fact]
    public void AppendToPeriodicFile_ResolvesDatedNote()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "今日日记",
            WriteMode = TargetWriteMode.AppendToPeriodicFile,
            PathTemplate = "01 Daily日常",
            FileNameTemplate = "{{date:yyyy-MM-dd}}",
        };

        var result = resolver.Resolve(target, Context());

        Assert.True(result.IsValid, result.Message);
        Assert.EndsWith("01 Daily日常\\2026-07-23.md", result.ResolvedPaths!.NotePath);
    }

    [Fact]
    public void StagingOnly_ReturnsEmptyWithoutNotePath()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "只暂存",
            WriteMode = TargetWriteMode.StagingOnly,
            PathTemplate = string.Empty,
        };

        var result = resolver.Resolve(target, Context());

        Assert.True(result.IsValid, result.Message);
        Assert.Null(result.ResolvedPaths!.NotePath);
        Assert.Empty(result.ResolvedPaths.ResolvedAttachments);
    }

    [Fact]
    public void ChineseSpacesAndEmojiPathsAreAccepted()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "收件箱 📥",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "收件箱 目录/子文件夹",
            FileNameTemplate = "{{title}}",
        };

        var result = resolver.Resolve(target, Context("中文标题 😀"));

        Assert.True(result.IsValid, result.Message);
        Assert.Contains("中文标题 😀", result.ResolvedPaths!.NotePath);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("a/../../b")]
    public void PathEscape_IsRejected(string pathTemplate)
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "逃逸",
            WriteMode = TargetWriteMode.AppendToFile,
            PathTemplate = pathTemplate,
        };

        var result = resolver.Resolve(target, Context());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AbsolutePath_IsRejected()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "绝对",
            WriteMode = TargetWriteMode.AppendToFile,
            PathTemplate = "E:\\other\\file",
        };

        var result = resolver.Resolve(target, Context());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SameNameNote_GeneratesSuffixWithoutOverwriting()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        Directory.CreateDirectory(Path.Combine(root.Path, "Inbox"));
        // 预先创建同名笔记。
        var existing = Path.Combine(root.Path, "Inbox", "2026-07-23-143045-冲突.md");
        File.WriteAllText(existing, "已有内容");

        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{timestamp}}-{{title}}",
        };

        var result = resolver.Resolve(target, Context("冲突"));

        Assert.True(result.IsValid, result.Message);
        Assert.EndsWith("-2.md", result.ResolvedPaths!.NotePath);
        Assert.NotEqual(existing, result.ResolvedPaths.NotePath);
        Assert.Equal("已有内容", File.ReadAllText(existing));
    }

    [Fact]
    public void SameNameAttachment_GeneratesSuffixWithoutOverwriting()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var attachmentDir = Path.Combine(root.Path, "Attachments", "2026-07-23");
        Directory.CreateDirectory(attachmentDir);
        var existing = Path.Combine(attachmentDir, "截图.png");
        File.WriteAllBytes(existing, [1, 2, 3]);

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
            new("截图.png", 100),
        };

        var result = resolver.Resolve(target, Context("笔记"), attachments);

        Assert.True(result.IsValid, result.Message);
        Assert.Single(result.ResolvedPaths!.ResolvedAttachments);
        Assert.EndsWith("截图-2.png", result.ResolvedPaths.ResolvedAttachments[0].AbsolutePath);
        // 原文件未被覆盖。
        Assert.Equal([1, 2, 3], File.ReadAllBytes(existing));
    }

    [Fact]
    public void BesideNoteAttachment_StaysWithinVault()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Notes/2026/07",
            FileNameTemplate = "{{title}}",
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.BesideNote,
                DirectoryTemplate = "assets",
            },
        };
        var attachments = new List<AttachmentInput> { new("a.txt", 10) };

        var result = resolver.Resolve(target, Context("标题"), attachments);

        Assert.True(result.IsValid, result.Message);
        Assert.NotNull(result.ResolvedPaths!.AttachmentDirectory);
        Assert.StartsWith(root.Path, result.ResolvedPaths.AttachmentDirectory);
        Assert.Contains("Notes\\2026\\07\\assets", result.ResolvedPaths.AttachmentDirectory);
    }

    [Fact]
    public void BesideNoteAttachment_EscapingVaultIsRejected()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Notes",
            FileNameTemplate = "{{title}}",
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.BesideNote,
                DirectoryTemplate = "../../../outside",
            },
        };
        var attachments = new List<AttachmentInput> { new("a.txt", 10) };

        var result = resolver.Resolve(target, Context("标题"), attachments);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void FollowObsidian_UsesPreResolvedDirectory()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FollowObsidian,
                FollowObsidianDirectory = "Obsidian/attachments",
            },
        };
        var attachments = new List<AttachmentInput> { new("a.png", 10) };

        var result = resolver.Resolve(target, Context("标题"), attachments);

        Assert.True(result.IsValid, result.Message);
        Assert.Contains("Obsidian\\attachments", result.ResolvedPaths!.AttachmentDirectory);
    }

    [Fact]
    public void FollowObsidian_WithoutDirectoryFails()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "{{title}}",
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = AttachmentPolicyKind.FollowObsidian,
                FollowObsidianDirectory = null,
            },
        };
        var attachments = new List<AttachmentInput> { new("a.png", 10) };

        var result = resolver.Resolve(target, Context("标题"), attachments);

        Assert.False(result.IsValid);
        Assert.Contains("Obsidian", result.Message);
    }

    [Fact]
    public void WindowsReservedFileName_IsRejected()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "收件箱",
            WriteMode = TargetWriteMode.CreateNote,
            PathTemplate = "Inbox",
            FileNameTemplate = "CON",
        };

        var result = resolver.Resolve(target, Context());

        Assert.False(result.IsValid);
        Assert.Contains("保留", result.Message);
    }

    [Fact]
    public void EmptyPathTemplate_ForNonStaging_IsRejected()
    {
        using var root = new TemporaryDirectory();
        var resolver = CreateResolver(root);
        var target = new CaptureTarget
        {
            Name = "空路径",
            WriteMode = TargetWriteMode.AppendToFile,
            PathTemplate = "   ",
        };

        var result = resolver.Resolve(target, Context());

        Assert.False(result.IsValid);
    }
}
