using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InboxDock.App.ViewModels;
using Forms = System.Windows.Forms;

namespace InboxDock.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel vm = new();
    private readonly Forms.NotifyIcon tray;
    private bool quit;
    private bool collapsed;

    public MainWindow()
    {
        InitializeComponent(); DataContext = vm;
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "InboxDock", Visible = true };
        tray.DoubleClick += (_, _) => Toggle();
        var menu = new Forms.ContextMenuStrip(); menu.Items.Add("显示 / 隐藏", null, (_, _) => Toggle()); menu.Items.Add("打开 Inbox", null, (_, _) => vm.OpenInbox()); menu.Items.Add("退出", null, (_, _) => { quit = true; tray.Visible = false; System.Windows.Application.Current.Shutdown(); }); tray.ContextMenuStrip = menu;
        Loaded += async (_, _) => await vm.InitializeAsync();
        Closing += (_, e) => { if (!quit) { e.Cancel = true; Hide(); } };
    }
    private void Toggle() { if (IsVisible) Hide(); else { Show(); Activate(); } }
    private void OnHeaderDrag(object s, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void OnPin(object s, RoutedEventArgs e) => Topmost = !Topmost;
    private void OnCollapse(object s, RoutedEventArgs e)
    {
        collapsed = !collapsed;
        AnimateSize(collapsed ? 56 : 340, collapsed ? 56 : 480);
    }

    private void AnimateSize(double targetWidth, double targetHeight)
    {
        var duration = TimeSpan.FromMilliseconds(160);
        var widthAnimation = new DoubleAnimation(ActualWidth, targetWidth, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        var heightAnimation = new DoubleAnimation(ActualHeight, targetHeight, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        BeginAnimation(WidthProperty, widthAnimation);
        BeginAnimation(HeightProperty, heightAnimation);
    }
    private async void OnSettings(object s, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog { Description = "选择包含 .obsidian 的 Vault 根目录", UseDescriptionForTitle = true }; if (d.ShowDialog() == Forms.DialogResult.OK) await vm.SetVaultAsync(d.SelectedPath); }
    private async void OnCaptureText(object s, RoutedEventArgs e) => await vm.CaptureTextAsync();
    private async void OnAppendDaily(object s, RoutedEventArgs e) => await vm.AppendDailyAsync();
    private async void OnUndo(object s, RoutedEventArgs e) => await vm.UndoAsync();
    private void OnDragEnter(object s, System.Windows.DragEventArgs e) { if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) DropZone.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(228,241,233)); }
    private void OnDragLeave(object s, System.Windows.DragEventArgs e) => DropZone.Background = System.Windows.Media.Brushes.White;
    private async void OnDrop(object s, System.Windows.DragEventArgs e) { DropZone.Background = System.Windows.Media.Brushes.White; if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files) await vm.CaptureFilesAsync(files); }
}
