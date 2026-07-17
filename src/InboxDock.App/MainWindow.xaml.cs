using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using InboxDock.App.Clipboard;
using InboxDock.App.ViewModels;
using InboxDock.App.Windowing;
using InboxDock.Core.Staging;
using InboxDock.Core.Windowing;
using MahApps.Metro.IconPacks;
using Forms = System.Windows.Forms;

namespace InboxDock.App;

public partial class MainWindow : Window
{
    private const double CollapsedSize = 52;
    private const double ExpandedWidth = 304;
    private const double ExpandedHeight = 408;
    private const double EdgeMargin = 8;

    private readonly MainViewModel vm = new();
    private readonly Forms.NotifyIcon tray;
    private readonly string windowStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "window-state.json");
    private bool quit;
    private bool collapsed;
    private bool animating;
    private DockEdge dockEdge = DockEdge.Right;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = vm;
        tray = CreateTrayIcon();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await vm.InitializeAsync();
        RestoreWindowPlacement();
        ApplyRoundedClip();
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var icon = Environment.ProcessPath is { } path
            ? System.Drawing.Icon.ExtractAssociatedIcon(path)
            : null;
        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon ?? System.Drawing.SystemIcons.Application,
            Text = "InboxDock",
            Visible = true,
        };
        notifyIcon.DoubleClick += (_, _) => ToggleWindow();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏", null, (_, _) => ToggleWindow());
        menu.Items.Add("打开 Inbox", null, (_, _) => vm.OpenInbox());
        menu.Items.Add("退出", null, (_, _) =>
        {
            quit = true;
            SaveWindowPlacement();
            notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });
        notifyIcon.ContextMenuStrip = menu;
        return notifyIcon;
    }

    private void ToggleWindow()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        Activate();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowPlacement();
        if (quit) return;
        e.Cancel = true;
        Hide();
    }

    private async void OnCollapsedPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || animating) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var start = Forms.Cursor.Position;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        var end = Forms.Cursor.Position;
        var isClick = PointerGesture.IsClick(
            start.X / dpi.DpiScaleX,
            start.Y / dpi.DpiScaleY,
            end.X / dpi.DpiScaleX,
            end.Y / dpi.DpiScaleY,
            4);
        if (isClick)
        {
            await ExpandAsync();
        }
        else
        {
            await SnapToNearestEdgeAsync();
        }
    }

    private async void OnExpandedHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || animating) return;
        try
        {
            DragMove();
            await SnapToNearestEdgeAsync();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnPin(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinIcon.Kind = Topmost ? PackIconLucideKind.Pin : PackIconLucideKind.PinOff;
    }

    private async void OnCollapse(object sender, RoutedEventArgs e) => await CollapseAsync();

    private async Task CollapseAsync()
    {
        if (collapsed || animating) return;
        var workArea = MonitorWorkArea.ForWindow(this);
        var current = CurrentRect();
        dockEdge = WindowDockCalculator.NearestEdge(current, workArea);
        var target = WindowDockCalculator.TargetRect(
            dockEdge,
            current,
            workArea,
            CollapsedSize,
            CollapsedSize,
            EdgeMargin);
        collapsed = true;
        await AnimateRectAsync(target, 180);
        ExpandedShell.Visibility = Visibility.Collapsed;
        CollapsedShell.Visibility = Visibility.Visible;
        SaveWindowPlacement();
    }

    private async Task ExpandAsync()
    {
        if (!collapsed || animating) return;
        var workArea = MonitorWorkArea.ForWindow(this);
        var target = WindowDockCalculator.TargetRect(
            dockEdge,
            CurrentRect(),
            workArea,
            ExpandedWidth,
            ExpandedHeight,
            EdgeMargin);
        CollapsedShell.Visibility = Visibility.Collapsed;
        ExpandedShell.Visibility = Visibility.Visible;
        collapsed = false;
        await AnimateRectAsync(target, 180);
        SaveWindowPlacement();
    }

    private async Task SnapToNearestEdgeAsync()
    {
        var workArea = MonitorWorkArea.ForWindow(this);
        var current = CurrentRect();
        dockEdge = WindowDockCalculator.NearestEdge(current, workArea);
        var target = WindowDockCalculator.TargetRect(
            dockEdge,
            current,
            workArea,
            collapsed ? CollapsedSize : ExpandedWidth,
            collapsed ? CollapsedSize : ExpandedHeight,
            EdgeMargin);
        await AnimateRectAsync(target, 140);
        SaveWindowPlacement();
    }

    private Task AnimateRectAsync(WindowRect target, int durationMilliseconds)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            SetWindowRect(target);
            return Task.CompletedTask;
        }

        animating = true;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);

        var left = new DoubleAnimation(Left, target.Left, duration) { EasingFunction = easing };
        var top = new DoubleAnimation(Top, target.Top, duration) { EasingFunction = easing };
        var width = new DoubleAnimation(ActualWidth, target.Width, duration) { EasingFunction = easing };
        var height = new DoubleAnimation(ActualHeight, target.Height, duration) { EasingFunction = easing };
        height.Completed += (_, _) =>
        {
            SetWindowRect(target);
            animating = false;
            completion.TrySetResult();
        };

        BeginAnimation(LeftProperty, left, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(TopProperty, top, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(WidthProperty, width, HandoffBehavior.SnapshotAndReplace);
        BeginAnimation(HeightProperty, height, HandoffBehavior.SnapshotAndReplace);
        return completion.Task;
    }

    private void SetWindowRect(WindowRect rect)
    {
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        Left = rect.Left;
        Top = rect.Top;
        Width = rect.Width;
        Height = rect.Height;
    }

    private WindowRect CurrentRect() => new(Left, Top, ActualWidth, ActualHeight);

    private async void OnSettings(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择包含 .obsidian 的 Vault 根目录",
            UseDescriptionForTitle = true,
        };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            await vm.SetVaultAsync(dialog.SelectedPath);
        }
    }

    private async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        ResetDropZone();
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            await vm.StageFilesAsync(files);
        }
    }

    private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
        DropZone.Background = (System.Windows.Media.Brush)FindResource("AccentSoft");
        DropZone.BorderBrush = (System.Windows.Media.Brush)FindResource("Accent");
        e.Effects = System.Windows.DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, System.Windows.DragEventArgs e) => ResetDropZone();

    private void ResetDropZone()
    {
        DropZone.Background = (System.Windows.Media.Brush)FindResource("PaperRaised");
        DropZone.BorderBrush = (System.Windows.Media.Brush)FindResource("LineStrong");
    }

    private async void OnDraftPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await vm.SubmitDraftAsync();
            return;
        }

        if (e.Key != Key.V || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        ClipboardMaterial material;
        try
        {
            material = ClipboardMaterialReader.Read();
        }
        catch (InvalidOperationException ex)
        {
            e.Handled = true;
            vm.StatusText = ex.Message;
            return;
        }

        switch (material)
        {
            case ClipboardFiles files:
                e.Handled = true;
                await vm.StageFilesAsync(files.Paths);
                break;
            case ClipboardImage image:
                e.Handled = true;
                await vm.StageClipboardImageAsync(image.Bitmap);
                break;
            case ClipboardLink link:
                e.Handled = true;
                await vm.StagePastedLinkAsync(link.Value);
                break;
            case ClipboardText text:
                e.Handled = true;
                var start = DraftEditor.SelectionStart;
                var existing = DraftEditor.Text;
                DraftEditor.Text = existing.Remove(start, DraftEditor.SelectionLength).Insert(start, text.Value);
                DraftEditor.SelectionStart = start + text.Value.Length;
                break;
        }
    }

    private void OnMaterialCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: StagedMaterial material }) vm.SelectMaterial(material);
    }

    private void OnRequestRemove(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is System.Windows.Controls.Button { Tag: StagedMaterial material }) vm.RequestRemove(material);
    }

    private async void OnConfirmSelected(object sender, RoutedEventArgs e) => await vm.ConfirmSelectedAsync();

    private async void OnDeferSelected(object sender, RoutedEventArgs e) => await vm.DeferSelectedAsync();

    private async void OnRemoveSelected(object sender, RoutedEventArgs e) => await vm.RemoveSelectedAsync();

    private void OnCancelDelete(object sender, RoutedEventArgs e) => vm.CancelDelete();

    private async void OnAppendDaily(object sender, RoutedEventArgs e) => await vm.AppendDailyAsync();

    private async void OnUndo(object sender, RoutedEventArgs e) => await vm.UndoAsync();

    private void OnMaterialCardLoaded(object sender, RoutedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation || sender is not FrameworkElement element) return;
        element.Opacity = 0;
        element.RenderTransform = new TranslateTransform(0, 8);
        var duration = TimeSpan.FromMilliseconds(170);
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
        ((TranslateTransform)element.RenderTransform).BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(8, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void OnConfirmationVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!SystemParameters.ClientAreaAnimation || e.NewValue is not true) return;
        ConfirmationSheet.Opacity = 0;
        ConfirmationSheet.RenderTransform = new TranslateTransform(0, 10);
        var duration = TimeSpan.FromMilliseconds(150);
        ConfirmationSheet.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration));
        ((TranslateTransform)ConfirmationSheet.RenderTransform).BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e) => ApplyRoundedClip();

    private void ApplyRoundedClip()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 14, 14);
        ExpandedShell.Clip = clip;
        CollapsedShell.Clip = clip.Clone();
    }

    private void RestoreWindowPlacement()
    {
        var workArea = MonitorWorkArea.ForWindow(this);
        var state = LoadWindowState();
        dockEdge = state?.Edge ?? DockEdge.Right;
        collapsed = state?.Collapsed ?? false;
        var initial = state is null
            ? new WindowRect(
                workArea.Right - ExpandedWidth - EdgeMargin,
                workArea.Top + ((workArea.Height - ExpandedHeight) / 2),
                ExpandedWidth,
                ExpandedHeight)
            : new WindowRect(
                state.Left,
                state.Top,
                collapsed ? CollapsedSize : ExpandedWidth,
                collapsed ? CollapsedSize : ExpandedHeight);
        var target = WindowDockCalculator.TargetRect(
            dockEdge,
            initial,
            workArea,
            collapsed ? CollapsedSize : ExpandedWidth,
            collapsed ? CollapsedSize : ExpandedHeight,
            EdgeMargin);
        SetWindowRect(target);
        ExpandedShell.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapsedShell.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
    }

    private SavedWindowState? LoadWindowState()
    {
        try
        {
            return File.Exists(windowStatePath)
                ? JsonSerializer.Deserialize<SavedWindowState>(File.ReadAllText(windowStatePath))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveWindowPlacement()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(windowStatePath)!);
            var temporary = windowStatePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new SavedWindowState(dockEdge, Left, Top, collapsed)));
            File.Move(temporary, windowStatePath, overwrite: true);
        }
        catch
        {
        }
    }

    private sealed record SavedWindowState(DockEdge Edge, double Left, double Top, bool Collapsed);
}
