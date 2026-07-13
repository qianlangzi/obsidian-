using CommunityToolkit.Mvvm.ComponentModel;
using InboxDock.Core.Capture;
using InboxDock.Core.Configuration;
using InboxDock.Core.Daily;
using InboxDock.Core.History;
using InboxDock.Core.Vault;

namespace InboxDock.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SettingsStore store = new();
    private InboxCaptureService? inbox;
    private DailyCaptureService? daily;
    private CaptureResult? last;
    [ObservableProperty] private string captureText = "";
    [ObservableProperty] private string dailyText = "";
    [ObservableProperty] private DailyCategory selectedCategory = DailyCategory.Learning;
    [ObservableProperty] private string statusText = "请先选择 Obsidian Vault";
    [ObservableProperty] private bool canUndo;
    public IReadOnlyList<DailyCategory> Categories { get; } = Enum.GetValues<DailyCategory>();

    public async Task InitializeAsync() { var result = await store.LoadAsync(); if (result.Settings is not null) Configure(result.Settings); }
    public async Task SetVaultAsync(string path) { var check = VaultValidator.Validate(path); if (!check.IsValid) { StatusText = check.Message; return; } var settings = AppSettings.CreateDefault(check.CanonicalPath!); await store.SaveAsync(settings); Configure(settings); StatusText = "Vault 已连接"; }
    public async Task CaptureTextAsync() { if (inbox is null || string.IsNullOrWhiteSpace(CaptureText)) return; await Run(async () => { last = await inbox.CaptureTextAsync(CaptureText); CaptureText = ""; StatusText = "已收进 Inbox"; CanUndo = true; }); }
    public async Task CaptureFilesAsync(IReadOnlyList<string> files) { if (inbox is null) return; await Run(async () => { last = await inbox.CaptureFilesAsync(files); StatusText = $"已收集 {files.Count} 个文件"; CanUndo = true; }); }
    public async Task AppendDailyAsync() { if (daily is null || string.IsNullOrWhiteSpace(DailyText)) return; await Run(async () => { last = await daily.AppendAsync(SelectedCategory, DailyText); DailyText = ""; StatusText = "已追加到今天的 Daily"; CanUndo = true; }); }
    public async Task UndoAsync() { if (last is null) return; var result = await new UndoService().UndoAsync(last); StatusText = result.Message; if (result.IsSuccess) { last = null; CanUndo = false; } }
    private void Configure(AppSettings settings) { var layout = new VaultLayout(settings); inbox = new(layout); daily = new(layout); StatusText = "拖入文件，或快速写下一条"; }
    private async Task Run(Func<Task> action) { try { await action(); } catch (Exception ex) { StatusText = ex.Message; } }
}
