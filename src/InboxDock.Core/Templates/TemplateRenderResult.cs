namespace InboxDock.Core.Templates;

/// <summary>模板渲染时发生的结构化错误。</summary>
public sealed record TemplateError(
    string Variable,
    int Position,
    string Message);

/// <summary>模板渲染结果。</summary>
public sealed record TemplateRenderResult
{
    public string RenderedText { get; init; } = string.Empty;

    public IReadOnlyList<TemplateError> Errors { get; init; } = [];

    public bool IsSuccess => Errors.Count == 0;

    public static TemplateRenderResult Success(string text) => new() { RenderedText = text };

    public static TemplateRenderResult Failed(string text, IReadOnlyList<TemplateError> errors) => new()
    {
        RenderedText = text,
        Errors = errors,
    };
}
