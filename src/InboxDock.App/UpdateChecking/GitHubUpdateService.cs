using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using InboxDock.App.Diagnostics;
using InboxDock.Core.UpdateChecking;

namespace InboxDock.App.UpdateChecking;

/// <summary>
/// 非阻塞 GitHub Releases 更新检查。通过 GitHub API 获取最新发布，
/// 不下载二进制，不阻塞 UI 线程。失败时静默降级。
/// </summary>
public sealed class GitHubUpdateService : IDisposable
{
    private const string GitHubApiBase = "https://api.github.com/repos";
    private const string DefaultRepo = "qianlangzi/InboxDock";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient httpClient;
    private readonly string repo;
    private readonly AppLog? log;
    private DateTimeOffset lastCheckTime;
    private UpdateCheckResult? lastResult;
    private bool disposed;

    /// <summary>更新可用时触发，参数为最新版本号和发布页面 URL。</summary>
    public event Action<UpdateCheckResult>? UpdateAvailable;

    /// <summary>检查完成时触发，无论是否有更新。</summary>
    public event Action<UpdateCheckResult>? CheckCompleted;

    public GitHubUpdateService(string? repo = null, AppLog? log = null)
    {
        this.repo = repo ?? DefaultRepo;
        this.log = log;
        this.httpClient = new HttpClient();
        this.httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("InboxDock", "update-checker"));
        this.httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        this.httpClient.Timeout = RequestTimeout;
    }

    /// <summary>上次检查结果。null 表示从未检查过。</summary>
    public UpdateCheckResult? LastResult => lastResult;

    /// <summary>是否应该检查更新（距上次检查超过 6 小时）。</summary>
    public bool ShouldCheck => lastResult is null
        || DateTimeOffset.Now - lastCheckTime >= CheckInterval;

    /// <summary>
    /// 异步检查 GitHub 最新发布。不阻塞调用线程。
    /// 网络错误时静默降级，不抛异常。
    /// </summary>
    public async Task<UpdateCheckResult?> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        if (disposed) return null;

        try
        {
            var url = $"{GitHubApiBase}/{repo}/releases/latest";
            log?.Info("UpdateCheck", $"Checking {url}");

            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                log?.Warn("UpdateCheck", $"GitHub API returned {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var release = GitHubRelease.TryParse(json);
            if (release is null)
            {
                log?.Warn("UpdateCheck", "Failed to parse release JSON");
                return null;
            }

            var now = DateTimeOffset.Now;
            var hasUpdate = UpdateCheckResult.IsNewerVersion(currentVersion, release.TagName);
            var result = hasUpdate
                ? UpdateCheckResult.UpdateAvailable_(currentVersion, release, now)
                : UpdateCheckResult.UpToDate(currentVersion, now);

            lastResult = result;
            lastCheckTime = now;

            log?.Info("UpdateCheck", $"Current {currentVersion}, latest {release.TagName}, update available: {hasUpdate}");

            CheckCompleted?.Invoke(result);
            if (hasUpdate)
            {
                UpdateAvailable?.Invoke(result);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException ex)
        {
            log?.Warn("UpdateCheck", $"Network error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            log?.Error("UpdateCheck", ex);
            return null;
        }
    }

    /// <summary>如果距上次检查超过间隔，自动触发后台检查。</summary>
    public async void CheckIfNeededAsync(string currentVersion)
    {
        if (!ShouldCheck) return;
        await CheckAsync(currentVersion);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        httpClient.Dispose();
    }
}
