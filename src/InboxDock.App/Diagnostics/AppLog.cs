using System.IO;
using System.Text;
using InboxDock.Core.Diagnostics;

namespace InboxDock.App.Diagnostics;

/// <summary>
/// 简单滚动日志。每个日志文件不超过 MaxFileSizeBytes，
/// 超过后滚动到下一个编号文件，最多保留 MaxFileCount 个文件。
/// 只记录版本、时间、操作类别和错误类型，不记录正文、URL 查询、文件内容和剪贴板。
/// </summary>
public sealed class AppLog : IDisposable
{
    private const long MaxFileSizeBytes = 256 * 1024; // 256 KB
    private const int MaxFileCount = 3;
    private const string LogFilePrefix = "app-";

    private readonly string logDirectory;
    private readonly object sync = new();
    private bool disposed;

    public AppLog(string logDirectory)
    {
        this.logDirectory = logDirectory;
        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch
        {
            // 目录创建失败时静默忽略，日志写入会捕获异常。
        }
    }

    /// <summary>记录一条信息级日志。</summary>
    public void Info(string category, string message)
    {
        Write("INFO", category, message);
    }

    /// <summary>记录一条警告级日志。错误消息会被截断，不记录完整异常堆栈中的私人路径。</summary>
    public void Warn(string category, string message)
    {
        Write("WARN", category, message);
    }

    /// <summary>记录一条错误级日志。记录异常类型和截断消息，不记录正文和文件内容。</summary>
    public void Error(string category, Exception exception)
    {
        var truncatedMessage = DiagnosticRedactor.TruncateError(exception.Message, 300);
        Write("ERROR", category, $"{exception.GetType().Name}: {truncatedMessage}");
    }

    /// <summary>记录一条错误级日志，只记录类型和截断消息。</summary>
    public void Error(string category, string errorType, string message)
    {
        var truncatedMessage = DiagnosticRedactor.TruncateError(message, 300);
        Write("ERROR", category, $"{errorType}: {truncatedMessage}");
    }

    /// <summary>获取当前日志目录路径。</summary>
    public string LogDirectory => logDirectory;

    private void Write(string level, string category, string message)
    {
        if (disposed) return;

        try
        {
            lock (sync)
            {
                var current = GetCurrentLogFile();
                if (File.Exists(current))
                {
                    var size = new FileInfo(current).Length;
                    if (size >= MaxFileSizeBytes)
                    {
                        Rotate();
                        current = GetCurrentLogFile();
                    }
                }

                var line = $"{DateTimeOffset.Now:O} [{level}] [{category}] {message}{Environment.NewLine}";
                File.AppendAllText(current, line, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志写入失败不阻断应用。
        }
    }

    private string GetCurrentLogFile() => Path.Combine(logDirectory, $"{LogFilePrefix}current.log");

    private void Rotate()
    {
        // 删除最老的文件
        var oldest = Path.Combine(logDirectory, $"{LogFilePrefix}{MaxFileCount - 1}.log");
        if (File.Exists(oldest)) File.Delete(oldest);

        // 向后滚动编号
        for (var i = MaxFileCount - 2; i >= 0; i--)
        {
            var current = i == 0
                ? GetCurrentLogFile()
                : Path.Combine(logDirectory, $"{LogFilePrefix}{i}.log");
            if (!File.Exists(current)) continue;
            var next = Path.Combine(logDirectory, $"{LogFilePrefix}{i + 1}.log");
            File.Move(current, next);
        }
    }

    public void Dispose()
    {
        disposed = true;
    }
}
