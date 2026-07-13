using InboxDock.Core.Capture;

namespace InboxDock.Core.Tests.Capture;

public sealed class SafeNameTests
{
    [Theory]
    [InlineData("理解 Java 接口", "理解 Java 接口")]
    [InlineData("A/B:C*D?", "A B C D")]
    [InlineData("   ", "快速记录")]
    public void FromText_SanitizesWithoutRemovingChinese(string input, string expected)
        => Assert.Equal(expected, SafeName.FromText(input));

    [Fact]
    public void AvailablePath_AddsNumericSuffixWithoutOverwriting()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "记录.md", "记录-2.md" };

        var result = SafeName.AvailableFileName("记录.md", existing.Contains);

        Assert.Equal("记录-3.md", result);
    }
}
