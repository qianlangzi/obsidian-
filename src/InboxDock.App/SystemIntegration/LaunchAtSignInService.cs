using System.IO;
using Microsoft.Win32;
using InboxDock.Core.SystemIntegration;

namespace InboxDock.App.SystemIntegration;

/// <summary>
/// 管理 Windows 登录启动项（当前用户级 Run 键）。不要求管理员权限。
/// 纯逻辑由 LaunchAtSignInCommand 处理；此类只负责注册表 IO。
/// </summary>
public sealed class LaunchAtSignInService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "InboxDock";

    /// <summary>读取当前注册的启动项值。未注册时返回 null。</summary>
    public string? ReadRegisteredValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) as string;
    }

    /// <summary>计算当前启动项状态。</summary>
    public LaunchAtSignInStatus GetStatus(bool enabledInSettings, string currentExecutablePath)
    {
        var registered = ReadRegisteredValue();
        return LaunchAtSignInCommand.ResolveStatus(enabledInSettings, registered, currentExecutablePath);
    }

    /// <summary>
    /// 开启登录启动项。注册成功返回 true。
    /// </summary>
    public bool Enable(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)) return false;
        if (!File.Exists(executablePath)) return false;

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null) return false;
            key.SetValue(ValueName, LaunchAtSignInCommand.BuildValue(executablePath), RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 关闭登录启动项。只移除 InboxDock 创建的值。
    /// </summary>
    public bool Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null) return true; // 键不存在，视为已关闭。
            var existing = key.GetValue(ValueName);
            if (existing is null) return true; // 值不存在，视为已关闭。

            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
