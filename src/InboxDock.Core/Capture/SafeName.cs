using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InboxDock.Core.Capture;

public static partial class SafeName
{
    private const int MaxTextElements = 40;

    /// <summary>Windows 保留设备名，不能作为文件名主干。</summary>
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private const int MaxWindowsPathLength = 260;

    public static string FromText(string? text)
    {
        var firstLine = (text ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(firstLine.Length);
        foreach (var character in firstLine)
        {
            builder.Append(invalid.Contains(character) ? ' ' : character);
        }

        var clean = Whitespace().Replace(builder.ToString(), " ").Trim().Trim('.');
        if (string.IsNullOrWhiteSpace(clean))
        {
            return "快速记录";
        }

        var indexes = StringInfo.ParseCombiningCharacters(clean);
        return indexes.Length <= MaxTextElements
            ? clean
            : clean[..indexes[MaxTextElements]].Trim();
    }

    public static string AvailableFileName(string desiredFileName, Func<string, bool> exists)
    {
        if (!exists(desiredFileName))
        {
            return desiredFileName;
        }

        var extension = Path.GetExtension(desiredFileName);
        var stem = Path.GetFileNameWithoutExtension(desiredFileName);
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{stem}-{suffix}{extension}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("无法生成可用文件名。");
    }

    /// <summary>检查文件名主干是否为 Windows 保留设备名（CON、PRN 等）。</summary>
    public static bool IsReservedWindowsName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return ReservedWindowsNames.Contains(stem);
    }

    /// <summary>检查文件名是否包含 Windows 非法字符。</summary>
    public static bool ContainsInvalidFileNameChars(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = Path.GetFileName(fileName);
        return name.Any(c => invalid.Contains(c));
    }

    /// <summary>检查绝对路径是否超过 Windows 默认最大长度。</summary>
    public static bool IsPathTooLong(string absolutePath) => absolutePath.Length > MaxWindowsPathLength;

    /// <summary>
    /// 校验文件名合法性。返回用户可读错误信息，合法时返回 null。
    /// </summary>
    public static string? ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "文件名不能为空。";
        }

        if (ContainsInvalidFileNameChars(fileName))
        {
            return "文件名包含非法字符。";
        }

        if (IsReservedWindowsName(fileName))
        {
            return "文件名使用了 Windows 保留名称。";
        }

        return null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
