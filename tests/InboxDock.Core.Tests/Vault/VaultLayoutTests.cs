using InboxDock.Core.Configuration;
using InboxDock.Core.Tests.Support;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Tests.Vault;

public sealed class VaultLayoutTests
{
    [Fact]
    public void Defaults_ResolveApprovedChineseFolders()
    {
        using var root = new TemporaryDirectory();
        var layout = new VaultLayout(AppSettings.CreateDefault(root.Path));

        Assert.Equal(System.IO.Path.Combine(root.Path, "00 Inbox收件箱"), layout.InboxDirectory);
        Assert.Equal(System.IO.Path.Combine(root.Path, "01 Daily日常"), layout.DailyDirectory);
        Assert.Equal(System.IO.Path.Combine(root.Path, "05 Resources", "Attachments"), layout.AttachmentsDirectory);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    public void Resolve_RejectsPathOutsideVault(string relativePath)
    {
        using var root = new TemporaryDirectory();
        var settings = AppSettings.CreateDefault(root.Path) with { InboxPath = relativePath };

        Assert.Throws<InvalidOperationException>(() => new VaultLayout(settings));
    }

    [Fact]
    public void Resolve_RejectsRootedChildPath()
    {
        using var root = new TemporaryDirectory();
        var settings = AppSettings.CreateDefault(root.Path) with { DailyPath = System.IO.Path.GetTempPath() };

        Assert.Throws<InvalidOperationException>(() => new VaultLayout(settings));
    }
}
