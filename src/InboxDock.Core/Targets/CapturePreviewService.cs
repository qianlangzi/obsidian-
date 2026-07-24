using System.Text;
using InboxDock.Core.Templates;

namespace InboxDock.Core.Targets;

/// <summary>
/// 组合材料、目标、模板和路径解析结果，生成无副作用的写入预览。
/// 预览不得创建目录、复制附件或写入文件。
/// </summary>
public sealed class CapturePreviewService(TargetPathResolver resolver)
{
    private readonly TargetPathResolver resolver = resolver;

    /// <summary>
    /// 生成写入预览。
    /// </summary>
    /// <param name="target">收集目标。</param>
    /// <param name="context">模板上下文。</param>
    /// <param name="attachments">附件输入列表，可为空。</param>
    /// <param name="lastConfirmedRevision">
    /// 该目标上次被用户确认时的 Revision。null 表示从未确认过（新目标）。
    /// </param>
    public CapturePreview Preview(
        CaptureTarget target,
        TemplateContext context,
        IReadOnlyList<AttachmentInput>? attachments = null,
        int? lastConfirmedRevision = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);

        var resolution = resolver.Resolve(target, context, attachments);
        if (!resolution.IsValid)
        {
            return CapturePreview.Invalid(target.Name, resolution.Message);
        }

        var paths = resolution.ResolvedPaths!;
        var markdown = RenderMarkdown(target, context, paths.ResolvedAttachments);
        if (markdown is null)
        {
            return CapturePreview.Invalid(target.Name, "内容模板渲染失败，请检查目标模板配置。");
        }

        var (requiresConfirmation, reason) = DetermineConfirmation(target, paths, lastConfirmedRevision);

        return new CapturePreview
        {
            TargetName = target.Name,
            NotePath = paths.NotePath,
            RelativeNotePath = paths.RelativeNotePath,
            AttachmentPaths = paths.ResolvedAttachments.Select(a => a.AbsolutePath).ToArray(),
            ResolvedAttachments = paths.ResolvedAttachments,
            Markdown = markdown,
            RequiresConfirmation = requiresConfirmation,
            ConfirmationReason = reason,
            IsValid = true,
        };
    }

    private static (bool Required, string? Reason) DetermineConfirmation(
        CaptureTarget target,
        ResolvedTargetPaths paths,
        int? lastConfirmedRevision)
    {
        if (target.WriteMode == TargetWriteMode.StagingOnly)
        {
            return (false, null);
        }

        if (lastConfirmedRevision is null)
        {
            return (true, "首次使用此目标，请确认写入位置。");
        }

        if (target.Revision > lastConfirmedRevision.Value)
        {
            return (true, "目标配置已修改，请确认写入位置。");
        }

        if (paths.HadNameCollision)
        {
            return (true, "存在同名文件，将使用带后缀的新文件名。");
        }

        return (false, null);
    }

    private static string? RenderMarkdown(
        CaptureTarget target,
        TemplateContext context,
        IReadOnlyList<ResolvedAttachment> resolvedAttachments)
    {
        if (target.WriteMode == TargetWriteMode.StagingOnly)
        {
            return string.Empty;
        }

        var renderContext = context with
        {
            Files = resolvedAttachments.Count == 0
                ? null
                : resolvedAttachments
                    .Select(a => new TemplateAttachmentFile(a.OriginalName, a.VaultRelativePath, a.SizeBytes))
                    .ToArray(),
            Target = target.Name,
        };

        if (!string.IsNullOrWhiteSpace(target.ContentTemplate))
        {
            var rendered = TemplateRenderer.Render(target.ContentTemplate, renderContext);
            return rendered.IsSuccess ? rendered.RenderedText : null;
        }

        return RenderDefaultMarkdown(target, renderContext);
    }

    private static string RenderDefaultMarkdown(CaptureTarget target, TemplateContext context)
    {
        var builder = new StringBuilder();
        var title = string.IsNullOrWhiteSpace(context.Title) ? "未命名" : context.Title;
        builder.AppendLine("---");
        builder.AppendLine("type: inboxdock");
        builder.AppendLine("status: unprocessed");
        builder.AppendLine($"created: {context.Now:yyyy-MM-ddTHH:mm:sszzz}");
        builder.AppendLine($"source: {context.Source ?? "inboxdock"}");
        builder.AppendLine($"target: {context.Target ?? target.Name}");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine($"# {title}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(context.Note))
        {
            builder.AppendLine("## 备注");
            builder.AppendLine();
            builder.AppendLine(context.Note.Trim());
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(context.Content))
        {
            builder.AppendLine("## 内容");
            builder.AppendLine();
            builder.AppendLine(context.Content.Trim());
            builder.AppendLine();
        }

        if (context.Files is not null && context.Files.Count > 0)
        {
            builder.AppendLine("## 附件");
            builder.AppendLine();
            foreach (var file in context.Files)
            {
                var prefix = IsImage(file.OriginalName) ? "!" : string.Empty;
                builder.Append("- ")
                    .Append(prefix)
                    .Append("[[")
                    .Append(file.VaultRelativePath.Replace('\\', '/'))
                    .Append("]]")
                    .AppendLine();
            }
        }

        return builder.ToString();
    }

    private static bool IsImage(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp" or ".svg";
    }
}
