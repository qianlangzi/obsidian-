using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using InboxDock.Core.Capture;

namespace InboxDock.App.Clipboard;

public abstract record ClipboardMaterial;

public sealed record ClipboardFiles(IReadOnlyList<string> Paths) : ClipboardMaterial;

public sealed record ClipboardImage(BitmapSource Bitmap) : ClipboardMaterial;

public sealed record ClipboardLink(string Value) : ClipboardMaterial;

public sealed record ClipboardText(string Value) : ClipboardMaterial;

public sealed record EmptyClipboard : ClipboardMaterial;

public static class ClipboardMaterialReader
{
    public static ClipboardMaterial Read()
    {
        try
        {
            var data = System.Windows.Clipboard.GetDataObject();
            if (data is null) return new EmptyClipboard();

            var files = data.GetDataPresent(System.Windows.DataFormats.FileDrop)
                ? data.GetData(System.Windows.DataFormats.FileDrop) as string[]
                : null;
            var bitmap = data.GetDataPresent(System.Windows.DataFormats.Bitmap)
                ? data.GetData(System.Windows.DataFormats.Bitmap) as BitmapSource
                : null;
            var text = data.GetDataPresent(System.Windows.DataFormats.UnicodeText)
                ? data.GetData(System.Windows.DataFormats.UnicodeText) as string
                : null;

            return ClipboardContentClassifier.Classify(files is { Length: > 0 }, bitmap is not null, text) switch
            {
                ClipboardContentKind.Files => new ClipboardFiles(files!),
                ClipboardContentKind.Image => new ClipboardImage(bitmap!),
                ClipboardContentKind.Link => new ClipboardLink(text!.Trim()),
                ClipboardContentKind.Text => new ClipboardText(text!),
                _ => new EmptyClipboard(),
            };
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException("剪贴板正被其他程序占用，请稍后重试。", ex);
        }
    }
}
