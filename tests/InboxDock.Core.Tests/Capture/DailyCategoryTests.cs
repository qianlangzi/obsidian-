using InboxDock.Core.Capture;

namespace InboxDock.Core.Tests.Capture;

public sealed class DailyCategoryTests
{
    [Theory]
    [InlineData(DailyCategory.Done, "完成")]
    [InlineData(DailyCategory.Learning, "学习")]
    [InlineData(DailyCategory.Problem, "问题")]
    [InlineData(DailyCategory.Idea, "灵感")]
    public void DisplayName_IsChinese(DailyCategory category, string expected)
        => Assert.Equal(expected, category.DisplayName());
}
