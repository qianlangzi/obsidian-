namespace InboxDock.Core.Vault;

public sealed record VaultValidationResult(bool IsValid, string? CanonicalPath, string Message);

public static class VaultValidator
{
    public static VaultValidationResult Validate(string? vaultPath)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
        {
            return new VaultValidationResult(false, null, "请选择 Obsidian Vault 文件夹。");
        }

        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(vaultPath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new VaultValidationResult(false, null, "Vault 路径无效。");
        }

        if (!Directory.Exists(canonicalPath))
        {
            return new VaultValidationResult(false, canonicalPath, "Vault 文件夹不存在。");
        }

        if (!Directory.Exists(Path.Combine(canonicalPath, ".obsidian")))
        {
            return new VaultValidationResult(false, canonicalPath, "所选文件夹不包含 .obsidian。请选择 Vault 根目录。");
        }

        return new VaultValidationResult(true, canonicalPath, "Vault 可用。");
    }
}
