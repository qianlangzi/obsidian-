using InboxDock.Core.SystemIntegration;

namespace InboxDock.Core.Tests.SystemIntegration;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void Default_IsCtrlShiftSpace()
    {
        Assert.Equal("Ctrl+Shift+Space", HotkeyGesture.Default.ToDisplayString());
        Assert.True(HotkeyGesture.Default.IsValid);
    }

    [Theory]
    [InlineData("Ctrl+Shift+Space", "Ctrl+Shift+Space")]
    [InlineData("ctrl+shift+space", "Ctrl+Shift+Space")]
    [InlineData("Ctrl+A", "Ctrl+A")]
    [InlineData("Alt+F4", "Alt+F4")]
    [InlineData("Shift+F1", "Shift+F1")]
    [InlineData("Ctrl+Alt+T", "Ctrl+Alt+T")]
    [InlineData("Ctrl+Win+D", "Ctrl+Win+D")]
    public void TryParse_ValidGesture_RoundTrips(string input, string expected)
    {
        var gesture = HotkeyGesture.TryParse(input);
        Assert.NotNull(gesture);
        Assert.Equal(expected, gesture!.ToDisplayString());
        Assert.True(gesture.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_EmptyOrNull_ReturnsNull(string? input)
    {
        Assert.Null(HotkeyGesture.TryParse(input!));
    }

    [Theory]
    [InlineData("Ctrl")]          // 只有修饰键，无主键
    [InlineData("Ctrl+Shift")]    // 多个修饰键，无主键
    [InlineData("Space")]          // 只有主键，无修饰键
    [InlineData("A")]             // 只有主键
    [InlineData("Ctrl+A+B")]      // 多个主键
    [InlineData("Ctrl+XYZ")]      // 非法主键
    [InlineData("Foo+Bar")]       // 全部非法
    public void TryParse_InvalidGesture_ReturnsNull(string input)
    {
        Assert.Null(HotkeyGesture.TryParse(input));
    }

    [Theory]
    [InlineData("Ctrl+Alt+Del", false)]   // Ctrl+Alt+Del 系统保留
    [InlineData("Ctrl+Alt+Delete", false)]
    [InlineData("Alt+Tab", false)]          // Alt+Tab 系统保留
    [InlineData("Win+D", false)]           // 仅 Win 修饰键被拒绝
    [InlineData("Win", false)]             // 仅 Win 无主键
    public void IsValid_RejectsSystemReserved(string input, bool expectedValid)
    {
        var gesture = HotkeyGesture.TryParse(input);
        if (gesture is null)
        {
            Assert.False(expectedValid);
            return;
        }
        Assert.Equal(expectedValid, gesture.IsValid);
    }

    [Theory]
    [InlineData("Ctrl+1", true)]
    [InlineData("Ctrl+0", true)]
    [InlineData("Ctrl+F12", true)]
    [InlineData("Ctrl+Enter", true)]
    [InlineData("Ctrl+Esc", true)]
    [InlineData("Ctrl+Up", true)]
    [InlineData("Ctrl+PageUp", true)]
    public void IsValid_AcceptsCommonKeys(string input, bool expectedValid)
    {
        var gesture = HotkeyGesture.TryParse(input);
        Assert.NotNull(gesture);
        Assert.Equal(expectedValid, gesture.IsValid);
    }

    [Fact]
    public void ToDisplayString_OrdersModifiersConsistently()
    {
        var gesture = HotkeyGesture.TryParse("Shift+Ctrl+A");
        Assert.NotNull(gesture);
        Assert.Equal("Ctrl+Shift+A", gesture!.ToDisplayString());
    }

    [Fact]
    public void Create_BuildsValidGesture()
    {
        var gesture = HotkeyGesture.Create(["Ctrl", "Alt"], "P");
        Assert.True(gesture.IsValid);
        Assert.Equal("Ctrl+Alt+P", gesture.ToDisplayString());
    }

    [Fact]
    public void TryParse_NormalizesKeyNameToUpperCase()
    {
        var gesture = HotkeyGesture.TryParse("Ctrl+a");
        Assert.NotNull(gesture);
        Assert.Equal("A", gesture!.Key);
    }

    [Fact]
    public void TryParse_NormalizesSpaceVariants()
    {
        var space = HotkeyGesture.TryParse("Ctrl+Spacebar");
        Assert.NotNull(space);
        Assert.Equal("Space", space!.Key);
    }
}
