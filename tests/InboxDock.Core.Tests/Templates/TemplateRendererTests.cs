using InboxDock.Core.Templates;

namespace InboxDock.Core.Tests.Templates;

public sealed class TemplateRendererTests
{
    private static readonly DateTimeOffset Clock =
        new(2026, 7, 23, 14, 30, 45, TimeSpan.FromHours(8));

    [Fact]
    public void RendersContentAndTitleVariables()
    {
        var context = new TemplateContext { Content = "这是正文", Title = "标题" };

        var result = TemplateRenderer.Render("## {{title}}\n\n{{content}}", context);

        Assert.True(result.IsSuccess);
        Assert.Equal("## 标题\n\n这是正文", result.RenderedText);
    }

    [Fact]
    public void RendersUrlNoteSourceAndTarget()
    {
        var context = new TemplateContext
        {
            Url = "https://example.com/page",
            Note = "备注内容",
            Source = "clipboard",
            Target = "收件箱",
        };

        var result = TemplateRenderer.Render("[{{target}}] {{url}} ({{source}}) — {{note}}", context);

        Assert.True(result.IsSuccess);
        Assert.Equal("[收件箱] https://example.com/page (clipboard) — 备注内容", result.RenderedText);
    }

    [Theory]
    [InlineData("{{date}}", "2026-07-23")]
    [InlineData("{{date:yyyy/MM/dd}}", "2026/07/23")]
    [InlineData("{{time}}", "14:30")]
    [InlineData("{{time:HH:mm:ss}}", "14:30:45")]
    [InlineData("{{timestamp}}", "2026-07-23-143045")]
    public void RendersDateAndTimeWithFormats(string template, string expected)
    {
        var context = new TemplateContext { Now = Clock };

        var result = TemplateRenderer.Render(template, context);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.RenderedText);
    }

    [Fact]
    public void UsesFixedClockRatherThanSystemTime()
    {
        var fixedClock = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = TemplateRenderer.Render("{{date:yyyy-MM-dd}}", new TemplateContext { Now = fixedClock });

        Assert.Equal("2020-01-01", result.RenderedText);
    }

    [Fact]
    public void UnknownVariableReturnsStructuredError()
    {
        var result = TemplateRenderer.Render("hello {{unknownVar}} world", TemplateContext.Empty);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("unknownVar", error.Variable);
        Assert.Equal("hello ".Length, error.Position);
        Assert.Contains("unknownVar", error.Message);
        Assert.Equal("hello  world", result.RenderedText);
    }

    [Fact]
    public void InvalidDateFormatReturnsError()
    {
        var result = TemplateRenderer.Render("{{date:yyyy-MM}}", new TemplateContext { Now = Clock });

        // yyyy-MM 是合法格式，应成功。改用真正非法的格式。
        Assert.True(result.IsSuccess);

        var bad = TemplateRenderer.Render("{{date:qq}}", new TemplateContext { Now = Clock });
        Assert.False(bad.IsSuccess);
        var error = Assert.Single(bad.Errors);
        Assert.Equal("date", error.Variable);
        Assert.Contains("格式", error.Message);
    }

    [Fact]
    public void DoesNotReparseSubstitutedContent()
    {
        // 用户正文里含有 {{date}}，替换后不应再次展开。
        var context = new TemplateContext { Content = "正文包含 {{date}} 不应展开" };

        var result = TemplateRenderer.Render("{{content}}", context);

        Assert.True(result.IsSuccess);
        Assert.Equal("正文包含 {{date}} 不应展开", result.RenderedText);
    }

    [Fact]
    public void RendersFilesAsStableMarkdownList()
    {
        var context = new TemplateContext
        {
            Files =
            [
                new TemplateAttachmentFile("截图.png", "Attachments/2026-07-23/截图.png", 1024),
                new TemplateAttachmentFile("文档.pdf", "Attachments/2026-07-23/文档.pdf", 5120),
            ],
        };

        var result = TemplateRenderer.Render("## 附件\n{{files}}", context);

        Assert.True(result.IsSuccess);
        Assert.Contains("![[Attachments/2026-07-23/截图.png]] · 1 KB", result.RenderedText);
        Assert.Contains("[[Attachments/2026-07-23/文档.pdf]] · 5 KB", result.RenderedText);
    }

    [Fact]
    public void FilesVariableWithoutFilesReturnsError()
    {
        var result = TemplateRenderer.Render("{{files}}", TemplateContext.Empty);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal("files", error.Variable);
        Assert.Contains("文件", error.Message);
    }

    [Fact]
    public void NullValuesSubstituteAsEmpty()
    {
        var result = TemplateRenderer.Render("[{{content}}][{{url}}][{{note}}]", TemplateContext.Empty);

        Assert.True(result.IsSuccess);
        Assert.Equal("[][][]", result.RenderedText);
    }

    [Fact]
    public void ChineseAndEmojiPathsRenderCorrectly()
    {
        var context = new TemplateContext
        {
            Title = "📎 收件箱",
            Content = "中文内容",
            Files =
            [
                new TemplateAttachmentFile("图片 😀.png", "收件箱/图片 😀.png", 2048),
            ],
        };

        var result = TemplateRenderer.Render("# {{title}}\n\n{{content}}\n\n{{files}}", context);

        Assert.True(result.IsSuccess);
        Assert.Contains("📎 收件箱", result.RenderedText);
        Assert.Contains("收件箱/图片 😀.png", result.RenderedText);
    }

    [Fact]
    public void MultipleErrorsAreAllReported()
    {
        var result = TemplateRenderer.Render("{{a}}{{date:qq}}{{b}}", TemplateContext.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Variable == "a");
        Assert.Contains(result.Errors, e => e.Variable == "date");
        Assert.Contains(result.Errors, e => e.Variable == "b");
    }
}
