using InboxDock.Core.Configuration;

namespace InboxDock.Core.Vault;

public sealed class VaultLayout
{
    public VaultLayout(AppSettings settings)
    {
        RootDirectory = Path.GetFullPath(settings.VaultPath);
        InboxDirectory = ResolveRelative(settings.InboxPath);
        DailyDirectory = ResolveRelative(settings.DailyPath);
        DailyTemplatePath = ResolveRelative(settings.DailyTemplatePath);
        AttachmentsDirectory = ResolveRelative(settings.AttachmentsPath);
    }

    public string RootDirectory { get; }

    public string InboxDirectory { get; }

    public string DailyDirectory { get; }

    public string DailyTemplatePath { get; }

    public string AttachmentsDirectory { get; }

    private string ResolveRelative(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException("Vault 内路径必须是非空相对路径。");
        }

        var resolved = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));
        var rootPrefix = RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("配置路径不能离开 Vault。");
        }

        return resolved;
    }
}
