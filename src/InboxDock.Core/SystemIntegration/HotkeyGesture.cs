using System.Text.Json.Serialization;

namespace InboxDock.Core.SystemIntegration;

/// <summary>
/// 全局快捷键手势。包含修饰键和主键，并提供往返字符串表示和有效性校验。
/// 字符串格式为 "Modifier+Modifier+Key"，例如 "Ctrl+Shift+Space"。
/// </summary>
public sealed record HotkeyGesture
{
    private static readonly HashSet<string> ModifierNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ctrl", "Alt", "Shift", "Win",
    };

    /// <summary>修饰键集合（不含主键）。Ctrl/Alt/Shift/Win。</summary>
    public IReadOnlySet<string> Modifiers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>主键名称，大写。例如 "Space"、"A"、"F1"。</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>解析快捷键字符串。无效时返回 null。</summary>
    public static HotkeyGesture? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        if (parts.Length == 0) return null;

        var modifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new List<string>();

        foreach (var part in parts)
        {
            if (ModifierNames.Contains(part))
            {
                modifiers.Add(CanonicalModifier(part));
            }
            else
            {
                keys.Add(part.ToUpperInvariant());
            }
        }

        if (keys.Count != 1) return null;
        if (modifiers.Count == 0) return null;

        var key = keys[0];
        if (!IsAllowedKey(key)) return null;

        var gesture = new HotkeyGesture
        {
            Modifiers = modifiers,
            Key = CanonicalKey(key),
        };
        if (!gesture.IsValid) return null;
        return gesture;
    }

    /// <summary>显式构造，跳过校验。用于已知有效的内部调用。</summary>
    public static HotkeyGesture Create(IEnumerable<string> modifiers, string key)
    {
        return new HotkeyGesture
        {
            Modifiers = new HashSet<string>(
                modifiers.Select(CanonicalModifier),
                StringComparer.OrdinalIgnoreCase),
            Key = CanonicalKey(key.ToUpperInvariant()),
        };
    }

    /// <summary>当前是否为有效组合。必须有主键和至少一个修饰键，且不含系统保留组合。</summary>
    public bool IsValid => !string.IsNullOrEmpty(Key)
                           && Modifiers.Count > 0
                           && !IsReserved();

    /// <summary>序列化为标准字符串。</summary>
    public string ToDisplayString()
    {
        var ordered = OrderModifiers();
        return string.Join("+", ordered.Concat([Key]));
    }

    /// <summary>默认快捷键 Ctrl+Shift+Space。</summary>
    public static HotkeyGesture Default { get; } = Create(["Ctrl", "Shift"], "Space");

    private static string CanonicalModifier(string name) => name.ToLowerInvariant() switch
    {
        "ctrl" => "Ctrl",
        "alt" => "Alt",
        "shift" => "Shift",
        "win" => "Win",
        _ => name,
    };

    /// <summary>将解析后的主键转换为用户友好的显示形式。内部统一为大写后查表。</summary>
    private static string CanonicalKey(string upperKey) => upperKey switch
    {
        "SPACE" or "SPACEBAR" => "Space",
        "ENTER" or "RETURN" => "Enter",
        "TAB" => "Tab",
        "ESC" or "ESCAPE" => "Esc",
        "BACK" or "BACKSPACE" => "Backspace",
        "DELETE" or "DEL" => "Delete",
        "INSERT" or "INS" => "Insert",
        "HOME" => "Home",
        "END" => "End",
        "PAGEUP" or "PGUP" => "PageUp",
        "PAGEDOWN" or "PGDN" => "PageDown",
        "UP" => "Up",
        "DOWN" => "Down",
        "LEFT" => "Left",
        "RIGHT" => "Right",
        "PRINTSCREEN" => "PrintScreen",
        "SCROLLLOCK" => "ScrollLock",
        "PAUSE" or "BREAK" => "Pause",
        _ => upperKey, // A-Z、0-9、F1-F24 保持原样
    };

    private IEnumerable<string> OrderModifiers()
    {
        var order = new[] { "Ctrl", "Alt", "Shift", "Win" };
        return order.Where(m => Modifiers.Contains(m));
    }

    private static bool IsAllowedKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;

        // A-Z
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z') return true;

        // 0-9
        if (key.Length == 1 && key[0] is >= '0' and <= '9') return true;

        // F1-F24
        if (key.StartsWith('F') && key.Length >= 2)
        {
            var suffix = key[1..];
            if (int.TryParse(suffix, out var n) && n >= 1 && n <= 24) return true;
        }

        // 常见命名键
        return key switch
        {
            "SPACE" or "SPACEBAR" => true,
            "ENTER" or "RETURN" => true,
            "TAB" => true,
            "ESC" or "ESCAPE" => true,
            "BACK" or "BACKSPACE" => true,
            "DELETE" or "DEL" => true,
            "INSERT" or "INS" => true,
            "HOME" => true,
            "END" => true,
            "PAGEUP" or "PGUP" => true,
            "PAGEDOWN" or "PGDN" => true,
            "UP" or "DOWN" or "LEFT" or "RIGHT" => true,
            "PRINTSCREEN" => true,
            "SCROLLLOCK" => true,
            "PAUSE" or "BREAK" => true,
            _ => false,
        };
    }

    private bool IsReserved()
    {
        // 系统保留组合，禁止使用。
        if (Modifiers.Contains("Ctrl") && Key == "Alt") return true;
        if (Modifiers.Contains("Ctrl") && Key == "Delete") return true;
        if (Modifiers.Count == 1 && Modifiers.Contains("Alt") && Key == "Tab") return true;
        if (Modifiers.Count == 1 && Modifiers.Contains("Win")) return true;
        if (Modifiers.Count == 0) return true;
        return false;
    }
}
