using InboxDock.Core.Tests.Support;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Tests.Vault;

public sealed class VaultValidatorTests
{
    [Fact]
    public void Validate_AcceptsDirectoryContainingObsidianFolder()
    {
        using var root = new TemporaryDirectory();
        Directory.CreateDirectory(System.IO.Path.Combine(root.Path, ".obsidian"));

        var result = VaultValidator.Validate(root.Path);

        Assert.True(result.IsValid);
        Assert.Equal(System.IO.Path.GetFullPath(root.Path), result.CanonicalPath);
    }

    [Fact]
    public void Validate_RejectsOrdinaryDirectory()
    {
        using var root = new TemporaryDirectory();

        var result = VaultValidator.Validate(root.Path);

        Assert.False(result.IsValid);
        Assert.Contains(".obsidian", result.Message);
    }
}
