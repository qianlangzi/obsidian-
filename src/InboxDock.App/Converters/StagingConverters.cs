using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using InboxDock.Core.Staging;
using InboxDock.Core.Targets;
using MahApps.Metro.IconPacks;

namespace InboxDock.App.Converters;

public sealed class MaterialKindIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        StagedMaterialKind.Files => PackIconLucideKind.Files,
        StagedMaterialKind.Link => PackIconLucideKind.Link,
        _ => PackIconLucideKind.Type,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class MaterialSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not StagedMaterial material) return string.Empty;
        if (material.Kind == StagedMaterialKind.Link) return "网页链接";
        if (material.Kind == StagedMaterialKind.Text) return "文字笔记";

        var bytes = material.Files.Sum(file => file.SizeBytes);
        var size = bytes switch
        {
            >= 1024 * 1024 => $"{bytes / 1024d / 1024d:0.#} MB",
            >= 1024 => $"{bytes / 1024d:0.#} KB",
            _ => $"{bytes} B",
        };
        return $"{material.Files.Count} 个文件 · {size}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class MaterialStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        StagedMaterialStatus.AwaitingConfirmation => "等待确认",
        StagedMaterialStatus.Deferred => "待处理",
        StagedMaterialStatus.Capturing => "正在收集",
        StagedMaterialStatus.Failed => "收集失败",
        _ => string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class MaterialStatusBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush Awaiting = Frozen("#9A641D");
    private static readonly System.Windows.Media.Brush Deferred = Frozen("#68716B");
    private static readonly System.Windows.Media.Brush Capturing = Frozen("#26734D");
    private static readonly System.Windows.Media.Brush Failed = Frozen("#B4473A");

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        StagedMaterialStatus.AwaitingConfirmation => Awaiting,
        StagedMaterialStatus.Deferred => Deferred,
        StagedMaterialStatus.Capturing => Capturing,
        StagedMaterialStatus.Failed => Failed,
        _ => Deferred,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static System.Windows.Media.Brush Frozen(string color)
    {
        var brush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}

public sealed class StepToVisibilityConverter : IValueConverter
{
    public int Step { get; set; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int step && step == Step ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class NonEmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>当 WriteMode 不是 StagingOnly 时显示路径模板。</summary>
public sealed class WriteModeToPathVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TargetWriteMode mode && mode != TargetWriteMode.StagingOnly
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>仅当 WriteMode 为 CreateNote 时显示文件名模板。</summary>
public sealed class WriteModeToFileNameVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TargetWriteMode mode && mode == TargetWriteMode.CreateNote
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>当附件策略需要目录模板时显示。</summary>
public sealed class AttachmentKindToDirectoryVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is AttachmentPolicyKind kind && kind != AttachmentPolicyKind.StagingOnly
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把 WriteMode 枚举翻译成中文标签。</summary>
public sealed class WriteModeToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TargetWriteMode mode ? mode switch
        {
            TargetWriteMode.AppendToFile => "追加到固定文件",
            TargetWriteMode.AppendToPeriodicFile => "追加到每日笔记",
            TargetWriteMode.CreateNote => "新建笔记",
            TargetWriteMode.StagingOnly => "只暂存",
            _ => mode.ToString(),
        } : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>批量模式开关按钮文案。true 时显示"完成"，false 时显示"批量"。</summary>
public sealed class BatchModeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "完成" : "批量";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>非零整数转为 bool，用于按钮 IsEnabled 绑定到选中数量。</summary>
public sealed class NonZeroBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i && i > 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>true 时折叠，false 时可见。用于互斥切换。</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool 转为"已开启"/"已关闭"文案。</summary>
public sealed class BoolToOnOffConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? "已开启" : "已关闭";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
