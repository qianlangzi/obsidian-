using System.Runtime.InteropServices;
using System.Windows.Interop;
using InboxDock.Core.SystemIntegration;

namespace InboxDock.App.SystemIntegration;

/// <summary>
/// 通过 Windows API 注册和注销全局快捷键。注册的消息通过 HwndSource 路由到 WPF 窗口。
/// </summary>
public sealed class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 9001;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? source;
    private HotkeyGesture? currentGesture;
    private bool disposed;

    /// <summary>快捷键触发时调用。通常在 UI 线程。</summary>
    public event Action? HotkeyPressed;

    /// <summary>当前快捷键是否已注册成功。</summary>
    public bool IsRegistered { get; private set; }

    /// <summary>绑定到指定窗口句柄，开始监听 WM_HOTKEY 消息。</summary>
    public void Bind(IntPtr windowHandle)
    {
        source = HwndSource.FromHwnd(windowHandle);
        if (source is not null)
        {
            source.AddHook(OnWindowMessage);
        }
    }

    /// <summary>
    /// 尝试注册快捷键。成功返回 true，失败返回 false。
    /// 更新快捷键时先注册新组合，成功后再释放旧组合。
    /// </summary>
    public bool TryRegister(HotkeyGesture gesture)
    {
        if (source is null) return false;
        if (!gesture.IsValid) return false;

        var modifiers = ComputeModifierFlags(gesture.Modifiers);
        var key = MapVirtualKey(gesture.Key);
        if (key == 0) return false;

        // 先尝试注册新组合；成功后再释放旧组合。
        if (!RegisterHotKey(source.Handle, HotkeyId, modifiers, key))
        {
            return false;
        }

        // 新组合注册成功，释放旧组合（如果有）。
        if (IsRegistered)
        {
            UnregisterHotKey(source.Handle, HotkeyId);
        }

        currentGesture = gesture;
        IsRegistered = true;
        return true;
    }

    /// <summary>释放当前注册的快捷键。</summary>
    public void Unregister()
    {
        if (!IsRegistered || source is null) return;
        UnregisterHotKey(source.Handle, HotkeyId);
        IsRegistered = false;
        currentGesture = null;
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ComputeModifierFlags(IReadOnlySet<string> modifiers)
    {
        uint flags = 0;
        if (modifiers.Contains("Ctrl")) flags |= 0x0002;  // MOD_CONTROL
        if (modifiers.Contains("Alt")) flags |= 0x0001;  // MOD_ALT
        if (modifiers.Contains("Shift")) flags |= 0x0004; // MOD_SHIFT
        if (modifiers.Contains("Win")) flags |= 0x0008;   // MOD_WIN
        return flags;
    }

    private static uint MapVirtualKey(string key)
    {
        // A-Z
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z')
        {
            return (uint)(key[0] - 'A' + 0x41);
        }

        // 0-9
        if (key.Length == 1 && key[0] is >= '0' and <= '9')
        {
            return (uint)(key[0] - '0' + 0x30);
        }

        // F1-F24
        if (key.StartsWith('F') && key.Length >= 2 && int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 24)
        {
            return (uint)(fn - 1 + 0x70); // VK_F1 = 0x70
        }

        return key switch
        {
            "Space" => 0x20,
            "Enter" => 0x0D,
            "Tab" => 0x09,
            "Esc" => 0x1B,
            "Backspace" => 0x08,
            "Delete" => 0x2E,
            "Insert" => 0x2D,
            "Home" => 0x24,
            "End" => 0x23,
            "PageUp" => 0x21,
            "PageDown" => 0x22,
            "Up" => 0x26,
            "Down" => 0x28,
            "Left" => 0x25,
            "Right" => 0x27,
            "PrintScreen" => 0x2C,
            "ScrollLock" => 0x91,
            "Pause" => 0x13,
            _ => 0,
        };
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Unregister();
        if (source is not null)
        {
            source.RemoveHook(OnWindowMessage);
        }
    }
}
