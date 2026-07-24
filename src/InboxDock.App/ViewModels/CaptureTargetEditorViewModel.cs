using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InboxDock.Core.Targets;

namespace InboxDock.App.ViewModels;

public sealed partial class CaptureTargetEditorViewModel : ObservableObject
{
    public CaptureTargetEditorViewModel()
    {
        WriteModes =
        [
            new(TargetWriteMode.AppendToFile, "追加到固定文件"),
            new(TargetWriteMode.AppendToPeriodicFile, "追加到每日笔记"),
            new(TargetWriteMode.CreateNote, "新建笔记"),
            new(TargetWriteMode.StagingOnly, "只暂存"),
        ];
        AttachmentKinds =
        [
            new(AttachmentPolicyKind.DatedDirectory, "按日期目录"),
            new(AttachmentPolicyKind.FixedDirectory, "固定目录"),
            new(AttachmentPolicyKind.BesideNote, "笔记旁目录"),
            new(AttachmentPolicyKind.StagingOnly, "只暂存"),
        ];
        SelectedWriteMode = WriteModes[0];
        SelectedAttachmentKind = AttachmentKinds[0];
    }

    public IReadOnlyList<WriteModeOption> WriteModes { get; }
    public IReadOnlyList<AttachmentKindOption> AttachmentKinds { get; }

    [ObservableProperty] private Guid id = Guid.NewGuid();
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string icon = string.Empty;
    [ObservableProperty] private WriteModeOption selectedWriteMode;
    [ObservableProperty] private string pathTemplate = "00 Inbox收件箱/收件箱.md";
    [ObservableProperty] private string fileNameTemplate = "{{timestamp}}-{{title}}";
    [ObservableProperty] private string contentTemplate = string.Empty;
    [ObservableProperty] private AttachmentKindOption selectedAttachmentKind;
    [ObservableProperty] private string attachmentDirectoryTemplate = "Attachments/{{date:yyyy-MM-dd}}";
    [ObservableProperty] private bool isAdvancedExpanded;

    /// <summary>是否为已有目标的编辑（用于决定是否递增 Revision）。</summary>
    [ObservableProperty] private bool isEditing;

    /// <summary>从 CaptureTarget 加载字段。</summary>
    public void LoadFrom(CaptureTarget target)
    {
        Id = target.Id;
        Name = target.Name;
        Icon = target.Icon;
        SelectedWriteMode = WriteModes.FirstOrDefault(w => w.Mode == target.WriteMode) ?? WriteModes[0];
        PathTemplate = target.PathTemplate;
        FileNameTemplate = target.FileNameTemplate;
        ContentTemplate = target.ContentTemplate;
        SelectedAttachmentKind = AttachmentKinds.FirstOrDefault(k => k.Kind == target.AttachmentPolicy.Kind) ?? AttachmentKinds[0];
        AttachmentDirectoryTemplate = target.AttachmentPolicy.DirectoryTemplate;
        IsEditing = true;
    }

    /// <summary>构建或更新 CaptureTarget。</summary>
    public CaptureTarget BuildTarget(bool incrementRevision = false)
    {
        var target = new CaptureTarget
        {
            Id = Id,
            Name = Name?.Trim() ?? string.Empty,
            Icon = Icon?.Trim() ?? string.Empty,
            WriteMode = SelectedWriteMode.Mode,
            PathTemplate = PathTemplate?.Trim() ?? string.Empty,
            FileNameTemplate = FileNameTemplate?.Trim() ?? string.Empty,
            ContentTemplate = ContentTemplate?.Trim() ?? string.Empty,
            AttachmentPolicy = new AttachmentPolicy
            {
                Kind = SelectedAttachmentKind.Kind,
                DirectoryTemplate = AttachmentDirectoryTemplate?.Trim() ?? string.Empty,
            },
            Revision = incrementRevision ? 1 : 0,
        };
        return target;
    }

    [RelayCommand]
    private void ToggleAdvanced()
    {
        IsAdvancedExpanded = !IsAdvancedExpanded;
    }
}

public sealed record WriteModeOption(TargetWriteMode Mode, string Label);
public sealed record AttachmentKindOption(AttachmentPolicyKind Kind, string Label);
