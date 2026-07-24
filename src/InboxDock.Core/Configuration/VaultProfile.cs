using System.Text.Json.Serialization;
using InboxDock.Core.Targets;

namespace InboxDock.Core.Configuration;

/// <summary>
/// 一个 Vault 的完整配置。v0.3.0 主界面只操作一个当前 Vault，
/// 但底层使用此模型为后续多 Vault 切换保留清晰边界。
/// </summary>
public sealed record VaultProfile
{
    /// <summary>稳定唯一标识。迁移旧配置时生成新 Id。</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>用户可见名称。</summary>
    public string Name { get; init; } = "默认 Vault";

    /// <summary>Vault 根目录绝对路径。</summary>
    public string VaultPath { get; init; } = string.Empty;

    /// <summary>默认收集目标 Id，必须指向 <see cref="CaptureTargets"/> 中存在的目标。</summary>
    public Guid? DefaultTargetId { get; init; }

    /// <summary>新建目标时使用的默认附件策略。</summary>
    public AttachmentPolicy DefaultAttachmentPolicy { get; init; } = AttachmentPolicy.DefaultDated;

    /// <summary>收集目标列表，不可为 null。</summary>
    public IReadOnlyList<CaptureTarget> CaptureTargets { get; init; } = [];

    public AppTheme Theme { get; init; } = AppTheme.System;

    public bool AlwaysOnTop { get; init; } = true;

    public bool LaunchAtSignIn { get; init; }

    /// <summary>
    /// 自动收回延时。null 表示永不自动收回，默认 5 秒。
    /// </summary>
    public TimeSpan? AutoHideDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>全局呼出快捷键字符串，默认 Ctrl+Shift+Space。</summary>
    public string GlobalHotkey { get; init; } = "Ctrl+Shift+Space";

    public WindowState WindowState { get; init; } = WindowState.Empty;

    /// <summary>校验配置完整性。校验只关心结构正确性，不检查 Vault 是否存在于磁盘。</summary>
    public VaultProfileValidation Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return VaultProfileValidation.Failed("Vault 名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(VaultPath))
        {
            return VaultProfileValidation.Failed("Vault 路径不能为空。");
        }

        var targets = CaptureTargets ?? [];
        if (targets.Count == 0 && DefaultTargetId is not null)
        {
            return VaultProfileValidation.Failed("默认目标指向了不存在的目标。");
        }

        var idSet = new HashSet<Guid>();
        foreach (var target in targets)
        {
            if (!idSet.Add(target.Id))
            {
                return VaultProfileValidation.Failed($"目标 Id 重复：{target.Id}。");
            }

            if (string.IsNullOrWhiteSpace(target.Name))
            {
                return VaultProfileValidation.Failed("目标名称不能为空。");
            }

            if (target.WriteMode != TargetWriteMode.StagingOnly && string.IsNullOrWhiteSpace(target.PathTemplate))
            {
                return VaultProfileValidation.Failed($"目标 {target.Name} 的路径不能为空。");
            }
        }

        if (DefaultTargetId is not null && !idSet.Contains(DefaultTargetId.Value))
        {
            return VaultProfileValidation.Failed("默认目标 Id 必须指向现有目标。");
        }

        return VaultProfileValidation.Success();
    }
}

public sealed record VaultProfileValidation(bool IsValid, string Message)
{
    public static VaultProfileValidation Success() => new(true, "配置有效。");

    public static VaultProfileValidation Failed(string message) => new(false, message);
}
