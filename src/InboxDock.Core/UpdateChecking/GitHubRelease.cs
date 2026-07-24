using System.Text.Json;
using System.Text.Json.Serialization;

namespace InboxDock.Core.UpdateChecking;

/// <summary>
/// 从 GitHub Releases API 解析的最新发布信息。
/// 只保留 InboxDock 需要的字段，不下载完整 release JSON。
/// </summary>
public sealed record GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string? Body { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    /// <summary>从 GitHub API JSON 响应解析最新发布。</summary>
    public static GitHubRelease? TryParse(string json, bool includePrerelease = false)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release is null) return null;
            // 草稿和预发布过滤。
            if (release.Draft) return null;
            if (release.Prerelease && !includePrerelease) return null;
            if (string.IsNullOrEmpty(release.TagName)) return null;
            return release;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>版本比较结果。</summary>
public sealed record UpdateCheckResult
{
    public required string CurrentVersion { get; init; }
    public required string LatestVersion { get; init; }
    public required bool UpdateAvailable { get; init; }
    public string? ReleaseUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public DateTimeOffset CheckedAt { get; init; }

    /// <summary>比较版本号。支持 v 前缀和 x.y.z 格式。</summary>
    public static bool IsNewerVersion(string current, string latest)
    {
        var currentVersion = ParseVersion(current);
        var latestVersion = ParseVersion(latest);

        if (currentVersion is null || latestVersion is null) return false;
        return latestVersion > currentVersion;
    }

    private static Version? ParseVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var trimmed = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(trimmed, out var v) ? v : null;
    }

    /// <summary>构建"无更新"结果。</summary>
    public static UpdateCheckResult UpToDate(string currentVersion, DateTimeOffset checkedAt) => new()
    {
        CurrentVersion = currentVersion,
        LatestVersion = currentVersion,
        UpdateAvailable = false,
        CheckedAt = checkedAt,
    };

    /// <summary>构建"有更新"结果。</summary>
    public static UpdateCheckResult UpdateAvailable_(
        string currentVersion,
        GitHubRelease release,
        DateTimeOffset checkedAt) => new()
    {
        CurrentVersion = currentVersion,
        LatestVersion = release.TagName,
        UpdateAvailable = true,
        ReleaseUrl = release.HtmlUrl,
        ReleaseNotes = release.Body,
        CheckedAt = checkedAt,
    };
}
