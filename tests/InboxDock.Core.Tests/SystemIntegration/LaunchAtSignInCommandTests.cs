using InboxDock.Core.SystemIntegration;

namespace InboxDock.Core.Tests.SystemIntegration;

public sealed class LaunchAtSignInCommandTests
{
    [Theory]
    [InlineData("C:\\App\\InboxDock.exe", "\"C:\\App\\InboxDock.exe\"")]
    [InlineData("C:\\Program Files\\My App\\InboxDock.exe", "\"C:\\Program Files\\My App\\InboxDock.exe\"")]
    [InlineData(" \"C:\\App\\InboxDock.exe\" ", "\"C:\\App\\InboxDock.exe\"")]
    [InlineData("\"C:\\App\\InboxDock.exe\"", "\"C:\\App\\InboxDock.exe\"")]
    public void BuildValue_WrapsPathWithQuotes(string input, string expected)
    {
        Assert.Equal(expected, LaunchAtSignInCommand.BuildValue(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildValue_EmptyThrows(string input)
    {
        Assert.Throws<ArgumentException>(() => LaunchAtSignInCommand.BuildValue(input));
    }

    [Theory]
    [InlineData("\"C:\\App\\InboxDock.exe\"", "C:\\App\\InboxDock.exe")]
    [InlineData("\"C:\\Program Files\\My App\\InboxDock.exe\"", "C:\\Program Files\\My App\\InboxDock.exe")]
    [InlineData("C:\\App\\InboxDock.exe", "C:\\App\\InboxDock.exe")]
    public void ExtractPath_ReturnsExecutablePath(string value, string expected)
    {
        Assert.Equal(expected, LaunchAtSignInCommand.ExtractPath(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractPath_EmptyReturnsNull(string? value)
    {
        Assert.Null(LaunchAtSignInCommand.ExtractPath(value));
    }

    [Fact]
    public void ExtractPath_UnterminatedQuoteReturnsNull()
    {
        Assert.Null(LaunchAtSignInCommand.ExtractPath("\"C:\\App\\InboxDock.exe"));
    }

    [Fact]
    public void IsCurrentPath_SamePathReturnsTrue()
    {
        var exe = "C:\\App\\InboxDock.exe";
        var value = LaunchAtSignInCommand.BuildValue(exe);
        Assert.True(LaunchAtSignInCommand.IsCurrentPath(value, exe));
    }

    [Fact]
    public void IsCurrentPath_DifferentPathReturnsFalse()
    {
        var registered = LaunchAtSignInCommand.BuildValue("C:\\Old\\InboxDock.exe");
        var current = "D:\\New\\InboxDock.exe";
        Assert.False(LaunchAtSignInCommand.IsCurrentPath(registered, current));
    }

    [Fact]
    public void IsCurrentPath_PathWithSpacesStillMatches()
    {
        var exe = "C:\\Program Files\\My App\\InboxDock.exe";
        var value = LaunchAtSignInCommand.BuildValue(exe);
        Assert.True(LaunchAtSignInCommand.IsCurrentPath(value, exe));
    }

    [Fact]
    public void IsCurrentPath_DifferentCaseStillMatches()
    {
        var value = LaunchAtSignInCommand.BuildValue("C:\\APP\\inboxdock.EXE");
        var current = "c:\\app\\InboxDock.exe";
        Assert.True(LaunchAtSignInCommand.IsCurrentPath(value, current));
    }

    [Fact]
    public void ResolveStatus_DisabledWhenOffAndUnregistered()
    {
        var status = LaunchAtSignInCommand.ResolveStatus(false, null, "C:\\App\\InboxDock.exe");
        Assert.Equal(LaunchAtSignInStatus.Disabled, status);
    }

    [Fact]
    public void ResolveStatus_EnabledWhenOnAndRegisteredMatches()
    {
        var exe = "C:\\App\\InboxDock.exe";
        var value = LaunchAtSignInCommand.BuildValue(exe);
        var status = LaunchAtSignInCommand.ResolveStatus(true, value, exe);
        Assert.Equal(LaunchAtSignInStatus.Enabled, status);
    }

    [Fact]
    public void ResolveStatus_NeedsRepairWhenOnButUnregistered()
    {
        var status = LaunchAtSignInCommand.ResolveStatus(true, null, "C:\\App\\InboxDock.exe");
        Assert.Equal(LaunchAtSignInStatus.NeedsRepair, status);
    }

    [Fact]
    public void ResolveStatus_NeedsRepairWhenPortableMoved()
    {
        var oldExe = "C:\\Old\\InboxDock.exe";
        var newExe = "D:\\New\\InboxDock.exe";
        var value = LaunchAtSignInCommand.BuildValue(oldExe);
        var status = LaunchAtSignInCommand.ResolveStatus(true, value, newExe);
        Assert.Equal(LaunchAtSignInStatus.NeedsRepair, status);
    }

    [Fact]
    public void ResolveStatus_NeedsRepairWhenOffButStillRegistered()
    {
        var exe = "C:\\App\\InboxDock.exe";
        var value = LaunchAtSignInCommand.BuildValue(exe);
        var status = LaunchAtSignInCommand.ResolveStatus(false, value, exe);
        Assert.Equal(LaunchAtSignInStatus.NeedsRepair, status);
    }
}
