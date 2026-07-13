using InboxDock.Core.Capture;
using InboxDock.Core.Markdown;

namespace InboxDock.Core.Tests.Markdown;

public sealed class DailyMarkdownTests
{
    [Fact]
    public void CreateFromTemplate_ReplacesDateAndTitle()
    {
        var result = DailyMarkdown.CreateFromTemplate("# {{title}}\ncreated: {{date:YYYY-MM-DD}}", new DateOnly(2026, 7, 13));

        Assert.Equal("# 2026-07-13\ncreated: 2026-07-13", result.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Append_AddsOwnedSectionAndMarker()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var result = DailyMarkdown.Append("# 2026-07-13\n", DailyCategory.Learning, "理解了接口", id, new TimeOnly(19, 30));

        Assert.Contains("## InboxDock 快速记录", result);
        Assert.Contains("- 19:30 · 学习 · 理解了接口 <!-- inboxdock:22222222-2222-2222-2222-222222222222 -->", result);
    }

    [Fact]
    public void Remove_OnlyDeletesMatchingCaptureLine()
    {
        var first = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var second = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var content = DailyMarkdown.Append("# Day\n", DailyCategory.Done, "第一条", first, new TimeOnly(9, 0));
        content = DailyMarkdown.Append(content, DailyCategory.Idea, "第二条", second, new TimeOnly(10, 0));

        var result = DailyMarkdown.Remove(content, first);

        Assert.DoesNotContain("第一条", result);
        Assert.Contains("第二条", result);
    }
}
