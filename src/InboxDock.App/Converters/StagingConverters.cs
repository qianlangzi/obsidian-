using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using InboxDock.Core.Staging;
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
