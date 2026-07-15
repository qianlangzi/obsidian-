using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using InboxDock.Core.Windowing;
using Forms = System.Windows.Forms;

namespace InboxDock.App.Windowing;

public static class MonitorWorkArea
{
    public static WindowRect ForWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var screen = handle != IntPtr.Zero && GetWindowRect(handle, out var bounds)
            ? Forms.Screen.FromPoint(new System.Drawing.Point(
                bounds.Left + ((bounds.Right - bounds.Left) / 2),
                bounds.Top + ((bounds.Bottom - bounds.Top) / 2)))
            : Forms.Screen.FromPoint(Forms.Cursor.Position);
        var dpi = VisualTreeHelper.GetDpi(window);
        var area = screen.WorkingArea;
        return new WindowRect(
            area.Left / dpi.DpiScaleX,
            area.Top / dpi.DpiScaleY,
            area.Width / dpi.DpiScaleX,
            area.Height / dpi.DpiScaleY);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rectangle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
