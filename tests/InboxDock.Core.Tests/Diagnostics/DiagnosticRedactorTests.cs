using InboxDock.Core.Diagnostics;

namespace InboxDock.Core.Tests.Diagnostics;

public sealed class DiagnosticRedactorTests
{
    [Theory]
    [InlineData("C:\\Users\\qianlang\\Documents\\Vault", "C:\\Users\\<user>\\Documents\\Vault")]
    [InlineData("C:\\Users\\qianlang\\AppData\\Local\\InboxDock\\staging.json", "C:\\Users\\<user>\\AppData\\Local\\<app>\\staging.json")]
    [InlineData("D:\\Vault", "D:\\Vault")]
    [InlineData("", "")]
    public void RedactPath_MasksUserInfo(string input, string expected)
    {
        Assert.Equal(expected, DiagnosticRedactor.RedactPath(input));
    }

    [Fact]
    public void RedactPath_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DiagnosticRedactor.RedactPath(null));
        Assert.Equal(string.Empty, DiagnosticRedactor.RedactPath(string.Empty));
    }

    [Fact]
    public void RedactPaths_ReplacesAllOccurrences()
    {
        var text = "写入到 C:\\Users\\test\\Vault\\Notes.md 失败，原文件在 C:\\Users\\test\\Vault\\Notes.md";
        var paths = new[] { "C:\\Users\\test\\Vault\\Notes.md" };

        var result = DiagnosticRedactor.RedactPaths(text, paths);

        Assert.Contains("C:\\Users\\<user>\\Vault\\Notes.md", result);
        Assert.DoesNotContain("C:\\Users\\test\\", result);
    }

    [Fact]
    public void RedactPaths_LongestFirst()
    {
        var text = "路径 C:\\Users\\me\\Vault 和 C:\\Users\\me\\Vault\\Sub\\file.md";
        var paths = new[] { "C:\\Users\\me\\Vault", "C:\\Users\\me\\Vault\\Sub\\file.md" };

        var result = DiagnosticRedactor.RedactPaths(text, paths);

        Assert.Contains("C:\\Users\\<user>\\Vault", result);
        Assert.Contains("C:\\Users\\<user>\\Vault\\Sub\\file.md", result);
    }

    [Theory]
    [InlineData("短错误", 500, "短错误")]
    [InlineData("", 500, "")]
    public void TruncateError_ShortMessageUnchanged(string input, int max, string expected)
    {
        Assert.Equal(expected, DiagnosticRedactor.TruncateError(input, max));
    }

    [Fact]
    public void TruncateError_LongMessageGetsTruncated()
    {
        var longMessage = new string('A', 600);
        var result = DiagnosticRedactor.TruncateError(longMessage, 100);

        Assert.True(result.Length <= 100 + "…[已截断]".Length);
        Assert.EndsWith("…[已截断]", result);
    }

    [Fact]
    public void TruncateError_NullReturnsEmpty()
    {
        Assert.Equal(string.Empty, DiagnosticRedactor.TruncateError(null));
    }

    [Fact]
    public void DiagnosticSnapshot_ToClipboardText_ContainsKeyInfo()
    {
        var snapshot = new DiagnosticSnapshot
        {
            AppVersion = "0.3.0",
            OperatingSystem = "Microsoft Windows 10",
            Architecture = "X64",
            RuntimeVersion = "10.0.0",
            VaultPath = "C:\\Users\\<user>\\Vault",
            VaultExists = true,
            VaultWritable = true,
            StagedItemCount = 3,
            LastErrorType = "IOException",
            CapturedAt = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero),
        };

        var text = snapshot.ToClipboardText();

        Assert.Contains("InboxDock 诊断信息", text);
        Assert.Contains("版本：0.3.0", text);
        Assert.Contains("系统：Microsoft Windows 10", text);
        Assert.Contains("架构：X64", text);
        Assert.Contains("暂存数量：3", text);
        Assert.Contains("最近错误类型：IOException", text);
    }

    [Fact]
    public void DiagnosticSnapshot_ToClipboardText_NoErrorTypeOmitsLine()
    {
        var snapshot = new DiagnosticSnapshot
        {
            AppVersion = "0.3.0",
            CapturedAt = DateTimeOffset.Now,
        };

        var text = snapshot.ToClipboardText();

        Assert.DoesNotContain("最近错误类型", text);
    }

    [Fact]
    public void DiagnosticSnapshot_Capture_RedactsVaultPath()
    {
        var snapshot = DiagnosticSnapshot.Capture(
            appVersion: "0.3.0",
            vaultPath: "C:\\Users\\testuser\\MyVault",
            vaultExists: true,
            vaultWritable: true,
            stagedItemCount: 0,
            lastErrorType: null);

        Assert.Contains("<user>", snapshot.VaultPath);
        Assert.DoesNotContain("testuser", snapshot.VaultPath);
    }

    [Fact]
    public void DiagnosticSnapshot_Capture_IncludesEnvironmentInfo()
    {
        var snapshot = DiagnosticSnapshot.Capture(
            appVersion: "0.3.0",
            vaultPath: null,
            vaultExists: false,
            vaultWritable: false,
            stagedItemCount: 0,
            lastErrorType: null);

        Assert.False(string.IsNullOrEmpty(snapshot.OperatingSystem));
        Assert.False(string.IsNullOrEmpty(snapshot.Architecture));
        Assert.False(string.IsNullOrEmpty(snapshot.RuntimeVersion));
    }
}
