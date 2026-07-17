using InboxDock.Core.Capture;
using InboxDock.Core.Markdown;

namespace InboxDock.Core.Tests.Markdown;

public sealed class InboxMarkdownTests
{
    [Fact]
    public void ForText_EmitsApprovedFrontmatterAndContent()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var time = new DateTimeOffset(2026, 7, 13, 20, 30, 0, TimeSpan.FromHours(8));

        var markdown = InboxMarkdown.ForText("学习记录", "理解了接口", id, time);

        Assert.Contains("type: inbox", markdown);
        Assert.Contains("status: unprocessed", markdown);
        Assert.Contains("capture_id: 11111111-1111-1111-1111-111111111111", markdown);
        Assert.Contains("# 学习记录", markdown);
        Assert.Contains("## 内容\n\n理解了接口", markdown.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ForFiles_UsesEmbedsForImagesAndLinksForOtherFiles()
    {
        var files = new[]
        {
            new CapturedAttachment("截图.png", "05 Resources/Attachments/2026-07-13/截图.png", 120),
            new CapturedAttachment("报告.pdf", "05 Resources/Attachments/2026-07-13/报告.pdf", 240),
        };

        var markdown = InboxMarkdown.ForFiles("材料 2 项", files, Guid.NewGuid(), DateTimeOffset.Now);

        Assert.Contains("![[05 Resources/Attachments/2026-07-13/截图.png]]", markdown);
        Assert.Contains("[[05 Resources/Attachments/2026-07-13/报告.pdf]]", markdown);
        Assert.Contains("240 B", markdown);
    }

    [Fact]
    public void ForFiles_WithNote_WritesNoteBeforeAttachments()
    {
        var files = new[]
        {
            new CapturedAttachment("报告.pdf", "05 Resources/Attachments/报告.pdf", 42),
        };

        var markdown = InboxMarkdown.ForFiles(
            "材料 1 项",
            files,
            Guid.NewGuid(),
            DateTimeOffset.Now,
            "先阅读第二章");

        var normalized = markdown.Replace("\r\n", "\n");
        Assert.Contains("## 备注\n\n先阅读第二章\n\n## 附件", normalized);
    }

    [Fact]
    public void ForFiles_WithoutNote_DoesNotWriteEmptyNoteHeading()
    {
        var files = new[]
        {
            new CapturedAttachment("报告.pdf", "05 Resources/Attachments/报告.pdf", 42),
        };

        var markdown = InboxMarkdown.ForFiles(
            "材料 1 项",
            files,
            Guid.NewGuid(),
            DateTimeOffset.Now,
            null);

        Assert.DoesNotContain("## 备注", markdown);
    }
}
