using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
    private const double ExpandedWidth = 304;
    private const double ExpandedHeight = 408;
    private const double EdgeMargin = 8;
    private const double PeekLongSide = 46;
    private const double PeekShortSide = 20;

    private readonly MainViewModel vm = new();
    private readonly AutoPeekController autoPeek = new(TimeSpan.FromSeconds(10), DateTimeOffset.Now);
    private readonly DispatcherTimer idleTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Forms.ContextMenuStrip trayMenu = new();
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private readonly string windowStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "window-state.json");

    private bool quit;
    private bool animating;
    private bool pointerInside;
    private bool dragActive;
    private bool settingsOpen;
    private bool resourcesDisposed;
    private DockEdge dockEdge = DockEdge.Right;
    private WindowDisplayState displayState = WindowDisplayState.Expanded;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = vm;

        var extracted = Environment.ProcessPath is { } path
            ? System.Drawing.Icon.ExtractAssociatedIcon(path)
            : null;
        trayIcon = extracted ?? (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
        tray = CreateTrayIcon();

        idleTimer.Tick += OnIdleTimerTick;
        Loaded += OnLoaded;
        Closing += OnClosing;
        PreviewMouseMove += OnUserMouseMove;
        PreviewMouseDown += OnUserMouseDown;
        PreviewMouseWheel += OnUserMouseWheel;
        PreviewKeyDown += OnUserKeyDown;
        GotKeyboardFocus += OnUserKeyboardFocus;
        MouseEnter += OnWindowMouseEnter;
        MouseLeave += OnWindowMouseLeave;
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await vm.InitializeAsync();
        RestoreWindowPlacement();
        ApplyRoundedClip();
        idleTimer.Start();
        UpdateAutoPeekPauseState();
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "InboxDock",
            Visible = true,
        };
        notifyIcon.DoubleClick += OnTrayDoubleClick;
        trayMenu.Items.Add("显示 / 隐藏", null, (_, _) => ToggleWindow());
        trayMenu.Items.Add("打开 Inbox", null, (_, _) => vm.OpenInbox());
        trayMenu.Items.Add("退出", null, (_, _) => RequestExit());
        notifyIcon.ContextMenuStrip = trayMenu;
        return notifyIcon;
    }

    private void OnTrayDoubleClick(object? sender, EventArgs e) => ToggleWindow();

    private void RequestExit()
    {
        quit = true;
        SaveWindowPlacement();
        tray.Visible = false;
        System.Windows.Application.Current.Shutdown();
    }

    private void ToggleWindow()
    {
        if (IsVisible)
        {
            idleTimer.Stop();
            Hide();
            return;
        }

        Show();
        Activate();
        RecordActivity();
        idleTimer.Start();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowPlacement();
        if (quit)
        {
            DisposeResources();
            return;
        }

        e.Cancel = true;
        idleTimer.Stop();
        Hide();
    }

    private async void OnPeekMouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => await ExpandFromPeekAsync();

    private async void OnExpandedHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || animating) return;
        dragActive = true;
        UpdateAutoPeekPauseState();
        try
        {
            DragMove();
            await SnapToNearestEdgeAsync();
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            dragActive = false;
            RecordActivity();
            UpdateAutoPeekPauseState();
        }
    }

    private void OnPin(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        PinIcon.Kind = Topmost ? PackIconLucideKind.Pin : PackIconLucideKind.PinOff;
        RecordActivity();
        SaveWindowPlacement();
    }

    private async void OnCollapse(object sender, RoutedEventArgs e) => await PeekAsync();

    private async Task PeekAsync()
    {
        if (displayState != WindowDisplayState.Expanded || animating) return;

        var workArea = MonitorWorkArea.ForWindow(this);
        var current = CurrentRect();
        dockEdge = WindowDockCalculator.NearestEdge(current, workArea);
        var target = WindowDockCalculator.PeekHandleRect(
            dockEdge,
            current,
            workArea,
            PeekLongSide,
            PeekShortSide);

        displayState = WindowDisplayState.Animating;
        autoPeek.SetPeeking(true, DateTimeOffset.Now);
        UpdatePeekIcon();
        await Task.WhenAll(
            AnimateRectAsync(target, 260),
            AnimateExpandedVisualAsync(1, 0, 1, 0.92, 220));

        ExpandedShell.Visibility = Visibility.Collapsed;
        PeekShell.Visibility = Visibility.Visible;
        ShellShadow.Visibility = Visibility.Collapsed;
        ResetExpandedVisual();
        displayState = WindowDisplayState.Peeking;
        SaveWindowPlacement();
    }

    private async Task ExpandFromPeekAsync()
    {
        if (displayState != WindowDisplayState.Peeking || animating) return;

        var workArea = MonitorWorkArea.ForWindow(this);
        var target = WindowDockCalculator.TargetRect(
            dockEdge,
            CurrentRect(),
            workArea,
            ExpandedWidth,
            ExpandedHeight,
            EdgeMargin);

        displayState = WindowDisplayState.Animating;
        PeekShell.Visibility = Visibility.Collapsed;
        ShellShadow.Visibility = Visibility.Visible;
        ExpandedShell.Visibility = Visibility.Visible;
        ExpandedShell.Opacity = 0;
        ExpandedScale.ScaleX = 0.92;
        ExpandedScale.ScaleY = 0.92;

        await Task.WhenAll(
            AnimateRectAsync(target, 260),
            AnimateExpandedVisualAsync(0, 1, 0.92, 1, 240));

        displayState = WindowDisplayState.Expanded;
        autoPeek.SetPeeking(false, DateTimeOffset.Now);
        RecordActivity();
        UpdateAutoPeekPauseState();
        SaveWindowPlacement();
    }

    private async Task SnapToNearestEdgeAsync()
    {
        if (displayState != WindowDisplayState.Expanded) return;
        var workArea = MonitorWorkArea.ForWindow(this);
        var current = CurrentRect();
        dockEdge = WindowDockCalculator.NearestEdge(current, workArea);
        var target = WindowDockCalculator.TargetRect(
            dockEdge,
            current,
            workArea,
            ExpandedWidth,
            ExpandedHeight,
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
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

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

    private Task AnimateExpandedVisualAsync(
        double opacityFrom,
        double opacityTo,
        double scaleFrom,
        double scaleTo,
        int durationMilliseconds)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            ExpandedShell.Opacity = opacityTo;
            ExpandedScale.ScaleX = scaleTo;
            ExpandedScale.ScaleY = scaleTo;
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var duration = TimeSpan.FromMilliseconds(durationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var opacity = new DoubleAnimation(opacityFrom, opacityTo, duration) { EasingFunction = easing };
        var scaleX = new DoubleAnimation(scaleFrom, scaleTo, duration) { EasingFunction = easing };
        var scaleY = new DoubleAnimation(scaleFrom, scaleTo, duration) { EasingFunction = easing };
        opacity.Completed += (_, _) => completion.TrySetResult();
        ExpandedShell.BeginAnimation(OpacityProperty, opacity, HandoffBehavior.SnapshotAndReplace);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX, HandoffBehavior.SnapshotAndReplace);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY, HandoffBehavior.SnapshotAndReplace);
        return completion.Task;
    }

    private void ResetExpandedVisual()
    {
        ExpandedShell.BeginAnimation(OpacityProperty, null);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        ExpandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        ExpandedShell.Opacity = 1;
        ExpandedScale.ScaleX = 1;
        ExpandedScale.ScaleY = 1;
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
        settingsOpen = true;
        UpdateAutoPeekPauseState();
        try
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
        finally
        {
            settingsOpen = false;
            RecordActivity();
            UpdateAutoPeekPauseState();
        }
    }

    private async void OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        ResetDropOverlay();
        dragActive = false;
        RecordActivity();
        UpdateAutoPeekPauseState();
        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] files)
        {
            e.Handled = true;
            await vm.StageFilesAsync(files);
        }
    }

    private void OnDragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;
        dragActive = true;
        RecordActivity();
        UpdateAutoPeekPauseState();
        DropOverlay.Visibility = Visibility.Visible;
        e.Effects = System.Windows.DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, System.Windows.DragEventArgs e)
    {
        dragActive = false;
        ResetDropOverlay();
        RecordActivity();
        UpdateAutoPeekPauseState();
    }

    private void ResetDropOverlay() => DropOverlay.Visibility = Visibility.Collapsed;

    private async void OnDraftPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        RecordActivity();
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
        RecordActivity();
        if (sender is Border { Tag: StagedMaterial material }) vm.SelectMaterial(material);
    }

    private void OnRequestRemove(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        e.Handled = true;
        if (sender is System.Windows.Controls.Button { Tag: StagedMaterial material }) vm.RequestRemove(material);
    }

    private async void OnConfirmSelected(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        await vm.ConfirmSelectedAsync();
    }

    private async void OnDeferSelected(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        await vm.DeferSelectedAsync();
    }

    private async void OnRemoveSelected(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        await vm.RemoveSelectedAsync();
    }

    private void OnCancelDelete(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        vm.CancelDelete();
    }

    private async void OnAppendDaily(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        await vm.AppendDailyAsync();
    }

    private async void OnUndo(object sender, RoutedEventArgs e)
    {
        RecordActivity();
        await vm.UndoAsync();
    }

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
        UpdateAutoPeekPauseState();
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
        var radius = Math.Min(14, Math.Min(ActualWidth, ActualHeight) / 2);
        var clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), radius, radius);
        ExpandedShell.Clip = clip;
        PeekShell.Clip = clip.Clone();
    }

    private async void OnIdleTimerTick(object? sender, EventArgs e)
    {
        UpdateAutoPeekPauseState();
        if (displayState == WindowDisplayState.Expanded
            && !animating
            && autoPeek.ShouldPeek(DateTimeOffset.Now))
        {
            await PeekAsync();
        }
    }

    private void OnUserMouseMove(object sender, System.Windows.Input.MouseEventArgs e) => RecordActivity();

    private void OnUserMouseDown(object sender, MouseButtonEventArgs e) => RecordActivity();

    private void OnUserMouseWheel(object sender, MouseWheelEventArgs e) => RecordActivity();

    private void OnUserKeyDown(object sender, System.Windows.Input.KeyEventArgs e) => RecordActivity();

    private void OnUserKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => RecordActivity();

    private void OnWindowMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        pointerInside = true;
        RecordActivity();
        UpdateAutoPeekPauseState();
    }

    private void OnWindowMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        pointerInside = false;
        RecordActivity();
        UpdateAutoPeekPauseState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsBusy) or nameof(MainViewModel.IsConfirmationOpen))
        {
            UpdateAutoPeekPauseState();
        }
    }

    private void RecordActivity()
    {
        if (displayState == WindowDisplayState.Expanded) autoPeek.RecordActivity(DateTimeOffset.Now);
    }

    private void UpdateAutoPeekPauseState()
    {
        if (displayState == WindowDisplayState.Peeking) return;
        var shouldPause = displayState == WindowDisplayState.Animating
                          || pointerInside
                          || dragActive
                          || settingsOpen
                          || vm.IsBusy
                          || vm.IsConfirmationOpen
                          || !IsVisible;
        if (shouldPause && !autoPeek.IsPaused)
        {
            autoPeek.Pause();
        }
        else if (!shouldPause && autoPeek.IsPaused)
        {
            autoPeek.Resume(DateTimeOffset.Now);
        }
    }

    private void UpdatePeekIcon()
    {
        PeekIcon.Kind = dockEdge switch
        {
            DockEdge.Left => PackIconLucideKind.ChevronRight,
            DockEdge.Right => PackIconLucideKind.ChevronLeft,
            DockEdge.Top => PackIconLucideKind.ChevronDown,
            DockEdge.Bottom => PackIconLucideKind.ChevronUp,
            _ => PackIconLucideKind.ChevronLeft,
        };
    }

    private void RestoreWindowPlacement()
    {
        var workArea = MonitorWorkArea.ForWindow(this);
        var state = LoadWindowState();
        dockEdge = state?.Edge ?? DockEdge.Right;
        var peeking = state?.Peeking ?? state?.Collapsed ?? false;
        Topmost = state?.Topmost ?? true;
        PinIcon.Kind = Topmost ? PackIconLucideKind.Pin : PackIconLucideKind.PinOff;

        var expanded = state is null
            ? new WindowRect(
                workArea.Right - ExpandedWidth - EdgeMargin,
                workArea.Top + ((workArea.Height - ExpandedHeight) / 2),
                ExpandedWidth,
                ExpandedHeight)
            : new WindowRect(state.Left, state.Top, ExpandedWidth, ExpandedHeight);

        if (peeking)
        {
            var target = WindowDockCalculator.PeekHandleRect(
                dockEdge,
                expanded,
                workArea,
                PeekLongSide,
                PeekShortSide);
            SetWindowRect(target);
            ExpandedShell.Visibility = Visibility.Collapsed;
            PeekShell.Visibility = Visibility.Visible;
            ShellShadow.Visibility = Visibility.Collapsed;
            displayState = WindowDisplayState.Peeking;
            autoPeek.SetPeeking(true, DateTimeOffset.Now);
            UpdatePeekIcon();
        }
        else
        {
            var target = WindowDockCalculator.TargetRect(
                dockEdge,
                expanded,
                workArea,
                ExpandedWidth,
                ExpandedHeight,
                EdgeMargin);
            SetWindowRect(target);
            ExpandedShell.Visibility = Visibility.Visible;
            PeekShell.Visibility = Visibility.Collapsed;
            ShellShadow.Visibility = Visibility.Visible;
            displayState = WindowDisplayState.Expanded;
            autoPeek.SetPeeking(false, DateTimeOffset.Now);
        }
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
            var state = new SavedWindowState
            {
                Edge = dockEdge,
                Left = Left,
                Top = Top,
                Peeking = displayState == WindowDisplayState.Peeking,
                Topmost = Topmost,
            };
            File.WriteAllText(temporary, JsonSerializer.Serialize(state));
            File.Move(temporary, windowStatePath, overwrite: true);
        }
        catch
        {
        }
    }

    private void DisposeResources()
    {
        if (resourcesDisposed) return;
        resourcesDisposed = true;
        idleTimer.Stop();
        idleTimer.Tick -= OnIdleTimerTick;
        Loaded -= OnLoaded;
        Closing -= OnClosing;
        PreviewMouseMove -= OnUserMouseMove;
        PreviewMouseDown -= OnUserMouseDown;
        PreviewMouseWheel -= OnUserMouseWheel;
        PreviewKeyDown -= OnUserKeyDown;
        GotKeyboardFocus -= OnUserKeyboardFocus;
        MouseEnter -= OnWindowMouseEnter;
        MouseLeave -= OnWindowMouseLeave;
        vm.PropertyChanged -= OnViewModelPropertyChanged;
        vm.Dispose();
        tray.DoubleClick -= OnTrayDoubleClick;
        tray.Visible = false;
        tray.Dispose();
        trayMenu.Dispose();
        trayIcon.Dispose();
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        ResetExpandedVisual();
    }

    private enum WindowDisplayState
    {
        Expanded,
        Peeking,
        Animating,
    }

    private sealed class SavedWindowState
    {
        public DockEdge Edge { get; init; } = DockEdge.Right;
        public double Left { get; init; }
        public double Top { get; init; }
        public bool? Peeking { get; init; }
        public bool? Collapsed { get; init; }
        public bool? Topmost { get; init; }
    }
}
