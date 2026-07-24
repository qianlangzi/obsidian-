using System.Windows;
using System.Windows.Input;
using InboxDock.App.ViewModels;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace InboxDock.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        HotkeyCaptureArea.IsVisibleChanged += OnHotkeyCaptureAreaVisibleChanged;
    }

    public event Action? Saved;

    private void OnHotkeyCaptureAreaVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is bool visible && visible)
        {
            HotkeyCaptureArea.Focus();
            Keyboard.Focus(HotkeyCaptureArea);
        }
    }

    private void OnHotkeyCaptureKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (!viewModel.IsCapturingHotkey) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape cancels capture.
        if (key == Key.Escape)
        {
            viewModel.CancelCaptureHotkeyCommand.Execute(null);
            return;
        }

        // Ignore pure modifier key presses; wait for actual key.
        if (IsModifierKey(key)) return;

        var modifiers = Keyboard.Modifiers;
        var parts = new List<string>(5);
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

        var keyName = MapKeyName(key);
        if (keyName is null) return;
        parts.Add(keyName);

        if (parts.Count <= 1) return; // No modifier pressed.

        var gestureText = string.Join("+", parts);
        viewModel.ApplyCapturedHotkey(gestureText);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin;

    private static string? MapKeyName(Key key) => key switch
    {
        Key.Space => "Space",
        Key.Enter => "Enter",
        Key.Tab => "Tab",
        Key.Escape => "Esc",
        Key.Back => "Backspace",
        Key.Delete => "Delete",
        Key.Insert => "Insert",
        Key.Home => "Home",
        Key.End => "End",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.Up => "Up",
        Key.Down => "Down",
        Key.Left => "Left",
        Key.Right => "Right",
        Key.PrintScreen => "PrintScreen",
        Key.Scroll => "ScrollLock",
        Key.Pause => "Pause",
        >= Key.A and <= Key.Z => key.ToString(),
        >= Key.D0 and <= Key.D9 => key.ToString()[1..],
        >= Key.F1 and <= Key.F24 => key.ToString(),
        _ => null,
    };

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            await viewModel.SaveAsync();
            Saved?.Invoke();
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ToggleAdvanced_Click(object sender, RoutedEventArgs e)
    {
        viewModel.Editor?.ToggleAdvancedCommand.Execute(null);
    }

    private void OnOpenLogDirectory(object sender, RoutedEventArgs e)
    {
        var error = viewModel.OpenLogDirectory();
        if (!string.IsNullOrEmpty(error))
        {
            System.Windows.MessageBox.Show(error, "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnOpenStagingDirectory(object sender, RoutedEventArgs e)
    {
        var error = viewModel.OpenStagingDirectory();
        if (!string.IsNullOrEmpty(error))
        {
            System.Windows.MessageBox.Show(error, "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var error = viewModel.CopyDiagnostics();
        if (!string.IsNullOrEmpty(error))
        {
            System.Windows.MessageBox.Show(error, "复制失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            System.Windows.MessageBox.Show("诊断信息已复制到剪贴板。", "已复制", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
