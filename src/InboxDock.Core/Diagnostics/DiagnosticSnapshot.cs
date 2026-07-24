using System.Runtime.InteropServices;

namespace InboxDock.Core.Diagnostics;

/// <summary>
/// 诊断快照。包含版本、系统、Vault 和暂存状态信息，供用户复制排查问题。
/// 不包含笔记正文、URL 查询内容、文件内容和剪贴板内容。
/// </summary>
public sealed record DiagnosticSnapshot
{
    /// <summary>应用版本。</summary>
    public string AppVersion { get; init; } = string.Empty;

    /// <summary>操作系统描述。</summary>
    public string OperatingSystem { get; init; } = string.Empty;

    /// <summary>进程架构（x64 / arm64 等）。</summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>.NET 运行时版本。</summary>
    public string RuntimeVersion { get; init; } = string.Empty;

    /// <summary>Vault 根目录（已遮蔽用户名）。</summary>
    public string VaultPath { get; init; } = string.Empty;

    /// <summary>Vault 是否存在于磁盘。</summary>
    public bool VaultExists { get; init; }

    /// <summary>Vault 是否可写。</summary>
    public bool VaultWritable { get; init; }

    /// <summary>暂存材料数量。</summary>
    public int StagedItemCount { get; init; }

    /// <summary>最近一次错误类型（不含正文）。</summary>
    public string? LastErrorType { get; init; }

    /// <summary>快照时间。</summary>
    public DateTimeOffset CapturedAt { get; init; }

    /// <summary>生成用户可复制的纯文本诊断信息。</summary>
    public string ToClipboardText()
    {
        var lines = new List<string>
        {
            $"InboxDock 诊断信息",
            $"时间：{CapturedAt:O}",
            $"版本：{AppVersion}",
            $"系统：{OperatingSystem}",
            $"架构：{Architecture}",
            $"运行时：{RuntimeVersion}",
            $"Vault：{(string.IsNullOrEmpty(VaultPath) ? "未配置" : VaultPath)}",
            $"Vault 存在：{(VaultExists ? "是" : "否")}",
            $"Vault 可写：{(VaultWritable ? "是" : "否")}",
            $"暂存数量：{StagedItemCount}",
        };

        if (!string.IsNullOrEmpty(LastErrorType))
        {
            lines.Add($"最近错误类型：{LastErrorType}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>从当前环境构建诊断快照。</summary>
    public static DiagnosticSnapshot Capture(
        string appVersion,
        string? vaultPath,
        bool vaultExists,
        bool vaultWritable,
        int stagedItemCount,
        string? lastErrorType,
        DateTimeOffset? capturedAt = null)
    {
        return new DiagnosticSnapshot
        {
            AppVersion = appVersion,
            OperatingSystem = Environment.OSVersion.ToString(),
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeVersion = Environment.Version.ToString(),
            VaultPath = DiagnosticRedactor.RedactPath(vaultPath),
            VaultExists = vaultExists,
            VaultWritable = vaultWritable,
            StagedItemCount = stagedItemCount,
            LastErrorType = lastErrorType,
            CapturedAt = capturedAt ?? DateTimeOffset.Now,
        };
    }
}
