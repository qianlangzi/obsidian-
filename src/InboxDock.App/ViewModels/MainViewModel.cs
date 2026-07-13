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
    [ObservableProperty] private CategoryOption selectedCategory;
    [ObservableProperty] private string statusText = "请先选择 Obsidian Vault";
    [ObservableProperty] private bool canUndo;
    [ObservableProperty] private bool hasVault;
    public IReadOnlyList<CategoryOption> Categories { get; } = Enum.GetValues<DailyCategory>().Select(value => new CategoryOption(value, value.DisplayName())).ToArray();

    public MainViewModel() => selectedCategory = Categories.Single(item => item.Value == DailyCategory.Learning);

    public async Task InitializeAsync() { var result = await store.LoadAsync(); if (result.Settings is not null) Configure(result.Settings); }
    public async Task SetVaultAsync(string path) { var check = VaultValidator.Validate(path); if (!check.IsValid) { StatusText = check.Message; return; } var settings = AppSettings.CreateDefault(check.CanonicalPath!); await store.SaveAsync(settings); Configure(settings); StatusText = "Vault 已连接"; }
    public async Task CaptureTextAsync() { if (inbox is null || string.IsNullOrWhiteSpace(CaptureText)) return; await Run(async () => { last = await inbox.CaptureTextAsync(CaptureText); CaptureText = ""; StatusText = "已收进 Inbox"; CanUndo = true; }); }
    public async Task CaptureFilesAsync(IReadOnlyList<string> files) { if (inbox is null) return; await Run(async () => { last = await inbox.CaptureFilesAsync(files); StatusText = $"已收集 {files.Count} 个文件"; CanUndo = true; }); }
    public async Task AppendDailyAsync() { if (daily is null || string.IsNullOrWhiteSpace(DailyText)) return; await Run(async () => { last = await daily.AppendAsync(SelectedCategory.Value, DailyText); DailyText = ""; StatusText = "已追加到今天的 Daily"; CanUndo = true; }); }
    public async Task UndoAsync() { if (last is null) return; var result = await new UndoService().UndoAsync(last); StatusText = result.Message; if (result.IsSuccess) { last = null; CanUndo = false; } }
    public void OpenInbox()
    {
        if (inbox is null || string.IsNullOrWhiteSpace(VaultPath)) return;
        var uri = $"obsidian://open?vault={Uri.EscapeDataString(System.IO.Path.GetFileName(VaultPath))}&file={Uri.EscapeDataString("00 Inbox收件箱")}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true });
    }

    [ObservableProperty] private string vaultPath = "";
    private void Configure(AppSettings settings) { var layout = new VaultLayout(settings); inbox = new(layout); daily = new(layout); VaultPath = layout.RootDirectory; HasVault = true; StatusText = "拖入文件，或快速写下一条"; }
    private async Task Run(Func<Task> action) { try { await action(); } catch (Exception ex) { StatusText = ex.Message; } }
}

public sealed record CategoryOption(DailyCategory Value, string Label);
