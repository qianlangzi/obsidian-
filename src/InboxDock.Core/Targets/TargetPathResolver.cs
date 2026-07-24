using InboxDock.Core.Capture;
using InboxDock.Core.Templates;
using InboxDock.Core.Vault;

namespace InboxDock.Core.Targets;

/// <summary>
/// 根据收集目标和模板上下文计算最终笔记与附件路径，并保证最终路径始终位于 Vault 内。
/// 解析器只读检查文件系统，不创建目录、不复制附件、不写入文件。
/// </summary>
public sealed class TargetPathResolver(string vaultRootDirectory)
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg",
    };

    public string VaultRoot { get; } = Path.GetFullPath(vaultRootDirectory);

    /// <summary>
    /// 解析目标的笔记和附件路径。
    /// </summary>
    /// <param name="target">收集目标。</param>
    /// <param name="context">模板上下文，提供日期、标题等变量。</param>
    /// <param name="attachments">待写入的附件输入列表，可为空。</param>
    public TargetValidationResult Resolve(
        CaptureTarget target,
        TemplateContext context,
        IReadOnlyList<AttachmentInput>? attachments = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);

        if (target.WriteMode == TargetWriteMode.StagingOnly)
        {
            return TargetValidationResult.Success(ResolvedTargetPaths.Empty(target.Name));
        }

        var noteResult = ResolveNoteRelativePath(target, context);
        if (noteResult.Error is not null) return TargetValidationResult.Failed(noteResult.Error);
        var noteRelative = noteResult.Relative;

        try
        {
            var noteAbsolute = VaultLayout.ResolveWithinVault(VaultRoot, noteRelative);
            var nameError = SafeName.ValidateFileName(Path.GetFileName(noteAbsolute));
            if (nameError is not null)
            {
                return TargetValidationResult.Failed($"笔记文件名无效：{nameError}");
            }

            if (SafeName.IsPathTooLong(noteAbsolute))
            {
                return TargetValidationResult.Failed("笔记路径过长。");
            }

            var hadCollision = noteResult.HadCollision;
            var resolved = ResolvedTargetPaths.Empty(target.Name) with
            {
                NotePath = noteAbsolute,
                RelativeNotePath = NormalizeRelative(noteRelative),
            };

            if (attachments is not null && attachments.Count > 0)
            {
                var attachmentResult = ResolveAttachments(target, noteRelative, context, attachments);
                if (attachmentResult.Error is not null)
                {
                    return TargetValidationResult.Failed(attachmentResult.Error);
                }

                hadCollision = hadCollision || attachmentResult.HadCollision;
                resolved = resolved with
                {
                    AttachmentDirectory = attachmentResult.Directory,
                    ResolvedAttachments = attachmentResult.Paths,
                };
            }

            resolved = resolved with { HadNameCollision = hadCollision };
            return TargetValidationResult.Success(resolved);
        }
        catch (InvalidOperationException exception)
        {
            return TargetValidationResult.Failed(exception.Message);
        }
    }

    private record NotePathResult(string Relative, string? Error, bool HadCollision);

    private NotePathResult ResolveNoteRelativePath(
        CaptureTarget target,
        TemplateContext context)
    {
        switch (target.WriteMode)
        {
            case TargetWriteMode.AppendToFile:
            {
                var rendered = TemplateRenderer.Render(target.PathTemplate, context);
                if (!rendered.IsSuccess)
                {
                    return new NotePathResult(string.Empty, $"目标路径模板渲染失败：{rendered.Errors[0].Message}", false);
                }

                var relative = EnsureMarkdownExtension(rendered.RenderedText.Trim().Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(relative))
                {
                    return new NotePathResult(string.Empty, "目标路径不能为空。", false);
                }

                return new NotePathResult(relative, null, false);
            }

            case TargetWriteMode.AppendToPeriodicFile:
            {
                var dirRendered = TemplateRenderer.Render(target.PathTemplate, context);
                if (!dirRendered.IsSuccess)
                {
                    return new NotePathResult(string.Empty, $"目标路径模板渲染失败：{dirRendered.Errors[0].Message}", false);
                }

                var directory = dirRendered.RenderedText.Trim().Replace('\\', '/').Trim('/');
                var nameRendered = TemplateRenderer.Render(
                    string.IsNullOrWhiteSpace(target.FileNameTemplate) ? "{{date:yyyy-MM-dd}}" : target.FileNameTemplate,
                    context);
                if (!nameRendered.IsSuccess)
                {
                    return new NotePathResult(string.Empty, $"文件名模板渲染失败：{nameRendered.Errors[0].Message}", false);
                }

                var fileName = EnsureMarkdownExtension(nameRendered.RenderedText.Trim());
                var nameError = SafeName.ValidateFileName(fileName);
                if (nameError is not null)
                {
                    return new NotePathResult(string.Empty, $"日记文件名无效：{nameError}", false);
                }

                return new NotePathResult($"{directory}/{fileName}", null, false);
            }

            case TargetWriteMode.CreateNote:
            {
                var dirRendered = TemplateRenderer.Render(target.PathTemplate, context);
                if (!dirRendered.IsSuccess)
                {
                    return new NotePathResult(string.Empty, $"目标目录模板渲染失败：{dirRendered.Errors[0].Message}", false);
                }

                var directory = dirRendered.RenderedText.Trim().Replace('\\', '/').Trim('/');
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return new NotePathResult(string.Empty, "目标目录不能为空。", false);
                }

                var nameRendered = TemplateRenderer.Render(
                    string.IsNullOrWhiteSpace(target.FileNameTemplate) ? "{{timestamp}}-{{title}}" : target.FileNameTemplate,
                    context);
                if (!nameRendered.IsSuccess)
                {
                    return new NotePathResult(string.Empty, $"文件名模板渲染失败：{nameRendered.Errors[0].Message}", false);
                }

                var rawName = nameRendered.RenderedText.Trim();
                var fileName = EnsureMarkdownExtension(string.IsNullOrWhiteSpace(rawName)
                    ? SafeName.FromText(rawName)
                    : rawName);
                var nameError = SafeName.ValidateFileName(fileName);
                if (nameError is not null)
                {
                    return new NotePathResult(string.Empty, $"笔记文件名无效：{nameError}", false);
                }

                var originalFileName = fileName;
                // 同名笔记生成 -2、-3 后缀，不覆盖已有文件。
                fileName = SafeName.AvailableFileName(fileName, candidateName =>
                    File.Exists(Path.Combine(VaultRoot, directory, candidateName)));
                var hadCollision = !string.Equals(originalFileName, fileName, StringComparison.Ordinal);
                return new NotePathResult($"{directory}/{fileName}", null, hadCollision);
            }

            default:
                return new NotePathResult(string.Empty, $"不支持的写入方式：{target.WriteMode}。", false);
        }
    }

    private AttachmentResolution ResolveAttachments(
        CaptureTarget target,
        string noteRelative,
        TemplateContext context,
        IReadOnlyList<AttachmentInput> attachments)
    {
        var policy = target.AttachmentPolicy;
        if (policy.Kind == AttachmentPolicyKind.StagingOnly)
        {
            return new AttachmentResolution(null, [], null, false);
        }

        string? attachmentDirectoryRelative;
        switch (policy.Kind)
        {
            case AttachmentPolicyKind.FollowObsidian:
                attachmentDirectoryRelative = policy.FollowObsidianDirectory;
                if (string.IsNullOrWhiteSpace(attachmentDirectoryRelative))
                {
                    return new AttachmentResolution(null, [], "未配置 Obsidian 附件目录，无法跟随。", false);
                }
                break;

            case AttachmentPolicyKind.FixedDirectory:
            case AttachmentPolicyKind.DatedDirectory:
            {
                var rendered = TemplateRenderer.Render(policy.DirectoryTemplate, context);
                if (!rendered.IsSuccess)
                {
                    return new AttachmentResolution(null, [], $"附件目录模板渲染失败：{rendered.Errors[0].Message}", false);
                }
                attachmentDirectoryRelative = rendered.RenderedText.Trim().Replace('\\', '/').Trim('/');
                break;
            }

            case AttachmentPolicyKind.BesideNote:
            {
                var noteDir = Path.GetDirectoryName(noteRelative)?.Replace('\\', '/') ?? string.Empty;
                var rendered = TemplateRenderer.Render(policy.DirectoryTemplate, context);
                if (!rendered.IsSuccess)
                {
                    return new AttachmentResolution(null, [], $"附件目录模板渲染失败：{rendered.Errors[0].Message}", false);
                }
                var sub = rendered.RenderedText.Trim().Replace('\\', '/').Trim('/');
                attachmentDirectoryRelative = string.IsNullOrEmpty(sub) ? noteDir : $"{noteDir}/{sub}";
                break;
            }

            default:
                return new AttachmentResolution(null, [], $"不支持的附件策略：{policy.Kind}。", false);
        }

        if (string.IsNullOrWhiteSpace(attachmentDirectoryRelative))
        {
            return new AttachmentResolution(null, [], "附件目录不能为空。", false);
        }

        try
        {
            var absoluteDir = VaultLayout.ResolveWithinVault(VaultRoot, attachmentDirectoryRelative);
            var resolved = new List<ResolvedAttachment>(attachments.Count);
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hadCollision = false;

            foreach (var input in attachments)
            {
                var desiredName = Path.GetFileName(input.OriginalName);
                var nameError = SafeName.ValidateFileName(desiredName);
                if (nameError is not null)
                {
                    return new AttachmentResolution(null, [], $"附件文件名无效（{input.OriginalName}）：{nameError}", false);
                }

                var available = SafeName.AvailableFileName(desiredName, candidate =>
                    usedNames.Contains(candidate) || File.Exists(Path.Combine(absoluteDir, candidate)));
                usedNames.Add(available);
                if (!string.Equals(desiredName, available, StringComparison.Ordinal)) hadCollision = true;

                var absolutePath = Path.Combine(absoluteDir, available);
                if (SafeName.IsPathTooLong(absolutePath))
                {
                    return new AttachmentResolution(null, [], $"附件路径过长（{input.OriginalName}）。", false);
                }

                resolved.Add(new ResolvedAttachment(
                    input.OriginalName,
                    absolutePath,
                    NormalizeRelative(Path.GetRelativePath(VaultRoot, absolutePath)),
                    input.SizeBytes));
            }

            return new AttachmentResolution(absoluteDir, resolved, null, hadCollision);
        }
        catch (InvalidOperationException exception)
        {
            return new AttachmentResolution(null, [], exception.Message, false);
        }
    }

    private static string EnsureMarkdownExtension(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ? path : path + ".md";
    }

    private static string NormalizeRelative(string relative) =>
        relative.Replace('\\', '/').TrimStart('/');

    private sealed record AttachmentResolution(
        string? Directory,
        IReadOnlyList<ResolvedAttachment> Paths,
        string? Error,
        bool HadCollision);
}
