using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InboxDock.Core.Capture;

public static partial class SafeName
{
    private const int MaxTextElements = 40;

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

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
