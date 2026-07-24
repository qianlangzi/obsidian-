using InboxDock.Core.Configuration;

namespace InboxDock.Core.Vault;

public sealed class VaultLayout
{
    public VaultLayout(AppSettings settings)
    {
        RootDirectory = Path.GetFullPath(settings.VaultPath);
        InboxDirectory = ResolveWithinVault(RootDirectory, settings.InboxPath);
        DailyDirectory = ResolveWithinVault(RootDirectory, settings.DailyPath);
        DailyTemplatePath = ResolveWithinVault(RootDirectory, settings.DailyTemplatePath);
        AttachmentsDirectory = ResolveWithinVault(RootDirectory, settings.AttachmentsPath);
    }

    public string RootDirectory { get; }

    public string InboxDirectory { get; }

    public string DailyDirectory { get; }

    public string DailyTemplatePath { get; }

    public string AttachmentsDirectory { get; }

    /// <summary>
    /// 校验 <paramref name="relativePath"/> 是非空相对路径，且解析后仍位于
    /// <paramref name="vaultRoot"/> 内。返回绝对路径。
    /// </summary>
    public static string ResolveWithinVault(string vaultRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Vault 内路径必须是非空相对路径。");
        }

        var resolved = Path.GetFullPath(Path.Combine(vaultRoot, relativePath));
        var rootPrefix = vaultRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("配置路径不能离开 Vault。");
        }

        return resolved;
    }
}
