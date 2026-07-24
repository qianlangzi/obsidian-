using System.Text;
using InboxDock.Core.IO;

namespace InboxDock.Core.Targets;

/// <summary>
/// 用一个安全写入流程覆盖固定文件追加、周期文件追加、新建笔记和只暂存。
/// 写入失败时回滚本次已创建内容；无法自动回滚的文件移入恢复目录。
/// </summary>
public sealed class TargetWriteService(string? recoveryRoot = null)
{
    private readonly string recoveryRoot = recoveryRoot ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "InboxDock",
        "Recovery");

    public async Task<TargetWriteResult> WriteAsync(
        TargetWriteRequest request,
        string vaultRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Target.WriteMode == TargetWriteMode.StagingOnly)
        {
            return TargetWriteResult.Success(request.CaptureId, TargetWriteMode.StagingOnly, null, null, []);
        }

        if (!request.Preview.IsValid)
        {
            return TargetWriteResult.Failed(request.CaptureId, request.Preview.UserErrorMessage ?? "预览无效，无法写入。");
        }

        if (!Directory.Exists(vaultRoot))
        {
            return TargetWriteResult.Failed(request.CaptureId, "Vault 目录不存在，无法写入。");
        }

        var createdFiles = new List<string>();
        var tempFiles = new List<string>();
        var captureId = request.CaptureId;
        var notePath = request.Preview.NotePath;
        var attachments = request.Preview.ResolvedAttachments;
        var sourceFiles = request.SourceFiles;

        try
        {
            // 阶段一：附件先复制到临时名称，全部完成后再提交。
            if (attachments.Count > 0)
            {
                if (sourceFiles.Count != attachments.Count)
                {
                    return TargetWriteResult.Failed(captureId, "附件源文件数量与解析结果不一致。");
                }

                // 先校验所有源文件存在，避免复制到一半才发现缺失导致临时文件泄漏。
                foreach (var source in sourceFiles)
                {
                    if (!File.Exists(source))
                    {
                        return TargetWriteResult.Failed(captureId, $"附件源文件不存在：{source}");
                    }
                }

                for (var i = 0; i < attachments.Count; i++)
                {
                    var source = sourceFiles[i];
                    var destination = attachments[i].AbsolutePath;
                    var directory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                    var temp = destination + $".{captureId:N}.copy.tmp";
                    await CopyFileAsync(source, temp, cancellationToken);
                    tempFiles.Add(temp);
                }

                // 阶段二：提交最终文件名。
                for (var i = 0; i < tempFiles.Count; i++)
                {
                    var temp = tempFiles[i];
                    var destination = attachments[i].AbsolutePath;
                    File.Move(temp, destination, overwrite: false);
                    createdFiles.Add(destination);
                }
            }

            // 写入笔记正文。
            if (notePath is not null)
            {
                AtomicFile.EnsureDirectory(notePath);
                var markdown = request.Preview.Markdown;

                if (request.Target.WriteMode is TargetWriteMode.AppendToFile
                    or TargetWriteMode.AppendToPeriodicFile)
                {
                    var existing = File.Exists(notePath)
                        ? await File.ReadAllTextAsync(notePath, cancellationToken)
                        : string.Empty;
                    var combined = existing.TrimEnd()
                        + Environment.NewLine
                        + Environment.NewLine
                        + WriteMarker.WrapAppend(markdown, captureId)
                        + Environment.NewLine;
                    await AtomicFile.ReplaceTextAsync(notePath, combined, cancellationToken);
                }
                else if (request.Target.WriteMode == TargetWriteMode.CreateNote)
                {
                    var stamped = WriteMarker.StampCreateNote(markdown, captureId);
                    await AtomicFile.WriteTextAsync(notePath, stamped, cancellationToken);
                }

                createdFiles.Add(notePath);
            }

            return TargetWriteResult.Success(
                captureId,
                request.Target.WriteMode,
                notePath,
                request.Preview.RelativeNotePath,
                attachments.Select(a => a.AbsolutePath).ToArray());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // 清理临时文件。
            foreach (var temp in tempFiles.Where(File.Exists))
            {
                try { File.Delete(temp); } catch { /* 清理失败忽略 */ }
            }

            // 回滚已创建的最终文件，移入恢复目录。
            var recoveryDir = Path.Combine(recoveryRoot, captureId.ToString("D"));
            foreach (var path in createdFiles.Where(File.Exists))
            {
                try
                {
                    Directory.CreateDirectory(recoveryDir);
                    var target = Path.Combine(recoveryDir, Path.GetFileName(path));
                    var index = 2;
                    while (File.Exists(target))
                    {
                        target = Path.Combine(recoveryDir,
                            $"{Path.GetFileNameWithoutExtension(path)}-{index++}{Path.GetExtension(path)}");
                    }
                    File.Move(path, target);
                }
                catch
                {
                    // 无法自动回滚的文件保留在原位，记录恢复目录供用户处理。
                }
            }

            return TargetWriteResult.Failed(
                captureId,
                $"写入失败，材料仍安全保存在 InboxDock：{exception.Message}",
                Directory.Exists(recoveryDir) ? recoveryDir : null);
        }
    }

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await input.CopyToAsync(output, cancellationToken);
    }
}
