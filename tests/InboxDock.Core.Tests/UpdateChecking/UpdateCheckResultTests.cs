using InboxDock.Core.UpdateChecking;

namespace InboxDock.Core.Tests.UpdateChecking;

public sealed class UpdateCheckResultTests
{
    [Theory]
    [InlineData("0.3.0", "0.4.0", true)]
    [InlineData("0.3.0", "0.3.0", false)]
    [InlineData("0.3.0", "0.2.0", false)]
    [InlineData("v0.3.0", "v0.4.0", true)]
    [InlineData("0.3.0", "0.3.1", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "2.0.0", true)]
    [InlineData("", "0.4.0", false)]
    [InlineData("0.3.0", "", false)]
    [InlineData("invalid", "0.4.0", false)]
    public void IsNewerVersion_ComparesCorrectly(string current, string latest, bool expected)
    {
        Assert.Equal(expected, UpdateCheckResult.IsNewerVersion(current, latest));
    }

    [Fact]
    public void TryParse_ValidJson_ReturnsRelease()
    {
        var json = """
        {
            "tag_name": "v0.4.0",
            "name": "InboxDock 0.4.0",
            "html_url": "https://github.com/user/repo/releases/tag/v0.4.0",
            "body": "Bug fixes",
            "prerelease": false,
            "draft": false
        }
        """;

        var release = GitHubRelease.TryParse(json);

        Assert.NotNull(release);
        Assert.Equal("v0.4.0", release!.TagName);
        Assert.Equal("https://github.com/user/repo/releases/tag/v0.4.0", release.HtmlUrl);
        Assert.False(release.Prerelease);
    }

    [Fact]
    public void TryParse_Draft_ReturnsNull()
    {
        var json = """{"tag_name": "v0.4.0", "draft": true, "prerelease": false}""";

        Assert.Null(GitHubRelease.TryParse(json));
    }

    [Fact]
    public void TryParse_Prerelease_ExcludedByDefault()
    {
        var json = """{"tag_name": "v0.4.0-beta", "draft": false, "prerelease": true}""";

        Assert.Null(GitHubRelease.TryParse(json));
    }

    [Fact]
    public void TryParse_Prerelease_IncludedWhenRequested()
    {
        var json = """{"tag_name": "v0.4.0-beta", "draft": false, "prerelease": true}""";

        var release = GitHubRelease.TryParse(json, includePrerelease: true);

        Assert.NotNull(release);
        Assert.Equal("v0.4.0-beta", release!.TagName);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsNull()
    {
        Assert.Null(GitHubRelease.TryParse("not json"));
        Assert.Null(GitHubRelease.TryParse(""));
        Assert.Null(GitHubRelease.TryParse("   "));
    }

    [Fact]
    public void TryParse_MissingTagName_ReturnsNull()
    {
        var json = """{"draft": false, "prerelease": false}""";

        Assert.Null(GitHubRelease.TryParse(json));
    }

    [Fact]
    public void UpToDate_BuildsCorrectResult()
    {
        var now = DateTimeOffset.Now;
        var result = UpdateCheckResult.UpToDate("0.3.0", now);

        Assert.False(result.UpdateAvailable);
        Assert.Equal("0.3.0", result.CurrentVersion);
        Assert.Equal("0.3.0", result.LatestVersion);
        Assert.Null(result.ReleaseUrl);
    }

    [Fact]
    public void UpdateAvailable_BuildsCorrectResult()
    {
        var now = DateTimeOffset.Now;
        var release = new GitHubRelease
        {
            TagName = "v0.4.0",
            HtmlUrl = "https://github.com/user/repo/releases/tag/v0.4.0",
            Body = "Release notes",
            Prerelease = false,
            Draft = false,
        };

        var result = UpdateCheckResult.UpdateAvailable_("0.3.0", release, now);

        Assert.True(result.UpdateAvailable);
        Assert.Equal("0.3.0", result.CurrentVersion);
        Assert.Equal("v0.4.0", result.LatestVersion);
        Assert.Equal("https://github.com/user/repo/releases/tag/v0.4.0", result.ReleaseUrl);
        Assert.Equal("Release notes", result.ReleaseNotes);
    }
}
