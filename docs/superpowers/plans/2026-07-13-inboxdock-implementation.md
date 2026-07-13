# InboxDock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and package a polished Windows desktop drop target that captures files, text, URLs, and Daily entries into an Obsidian Vault while Obsidian is closed.

**Architecture:** A dependency-free `InboxDock.Core` library owns Vault validation and all filesystem transactions. A thin .NET 10 WPF app uses MVVM for UI state, native Windows tray and startup integration, and delegates every knowledge operation to Core. xUnit unit and integration tests exercise Core against temporary Vaults before UI wiring.

**Tech Stack:** .NET 10, C#, WPF, CommunityToolkit.Mvvm 8.4.2, MahApps.Metro.IconPacks.Lucide 6.2.1, xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.7.0, GitHub Actions.

---

### Task 1: Scaffold the solution and repository baseline

**Files:**
- Create: `InboxDock.slnx`
- Create: `src/InboxDock.Core/InboxDock.Core.csproj`
- Create: `src/InboxDock.App/InboxDock.App.csproj`
- Create: `tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj`
- Create: `tests/InboxDock.IntegrationTests/InboxDock.IntegrationTests.csproj`
- Create: `.gitignore`
- Create: `Directory.Build.props`

- [ ] **Step 1: Scaffold projects with .NET 10**

```powershell
dotnet new sln -n InboxDock --format slnx
dotnet new classlib -n InboxDock.Core -o src/InboxDock.Core -f net10.0
dotnet new wpf -n InboxDock.App -o src/InboxDock.App -f net10.0
dotnet new xunit -n InboxDock.Core.Tests -o tests/InboxDock.Core.Tests -f net10.0
dotnet new xunit -n InboxDock.IntegrationTests -o tests/InboxDock.IntegrationTests -f net10.0
dotnet sln InboxDock.slnx add src/InboxDock.Core src/InboxDock.App tests/InboxDock.Core.Tests tests/InboxDock.IntegrationTests
```

- [ ] **Step 2: Add references and pinned packages**

```powershell
dotnet add src/InboxDock.App reference src/InboxDock.Core
dotnet add tests/InboxDock.Core.Tests reference src/InboxDock.Core
dotnet add tests/InboxDock.IntegrationTests reference src/InboxDock.Core
dotnet add src/InboxDock.App package CommunityToolkit.Mvvm --version 8.4.2
dotnet add src/InboxDock.App package MahApps.Metro.IconPacks.Lucide --version 6.2.1
```

- [ ] **Step 3: Add strict shared build settings**

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Set `UseWPF`, `UseWindowsForms`, `PublishSingleFile`, `SelfContained`, `RuntimeIdentifier=win-x64`, and `IncludeNativeLibrariesForSelfExtract` in the app project. Add standard .NET, Visual Studio, publish, and local settings exclusions to `.gitignore`.

- [ ] **Step 4: Build the empty solution**

Run: `dotnet build InboxDock.slnx`

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 5: Commit baseline**

```powershell
git add .
git commit -m "build: scaffold InboxDock solution"
```

### Task 2: Implement configuration and Vault path safety with TDD

**Files:**
- Create: `src/InboxDock.Core/Configuration/AppSettings.cs`
- Create: `src/InboxDock.Core/Configuration/SettingsStore.cs`
- Create: `src/InboxDock.Core/Vault/VaultLayout.cs`
- Create: `src/InboxDock.Core/Vault/VaultValidator.cs`
- Create: `tests/InboxDock.Core.Tests/Vault/VaultValidatorTests.cs`
- Create: `tests/InboxDock.Core.Tests/Configuration/SettingsStoreTests.cs`

- [ ] **Step 1: Write failing Vault validation tests**

Tests must assert that a valid root contains `.obsidian`, defaults resolve to the approved Chinese paths, `..` and rooted relative paths are rejected, and every resolved location remains under the canonical Vault root.

```csharp
[Fact]
public void Resolve_RejectsPathOutsideVault()
{
    using var vault = TestVault.Create();
    var layout = VaultLayout.Create(vault.Path, inboxPath: "..\\outside");
    Assert.Throws<InvalidOperationException>(() => layout.ResolveInbox());
}
```

- [ ] **Step 2: Verify the tests fail before implementation**

Run: `dotnet test tests/InboxDock.Core.Tests --filter "FullyQualifiedName~Vault"`

Expected: compile failure because `VaultLayout` and `VaultValidator` do not exist.

- [ ] **Step 3: Implement immutable settings and canonical path checks**

`AppSettings` contains Vault, Inbox, Daily, Daily template, Attachments, always-on-top, launch-at-sign-in, theme, and window coordinates. `VaultLayout` uses `Path.GetFullPath`, a trailing separator comparison, and `StringComparison.OrdinalIgnoreCase` to prove containment before returning a path.

- [ ] **Step 4: Implement atomic local settings storage**

`SettingsStore` reads and writes `%LocalAppData%\InboxDock\settings.json` using `System.Text.Json`. Saves write `settings.json.tmp`, flush, and rename. Invalid JSON returns a recoverable result rather than silently replacing the file.

- [ ] **Step 5: Run focused and full tests**

Run: `dotnet test InboxDock.slnx`

Expected: all tests pass.

- [ ] **Step 6: Commit configuration**

```powershell
git add src/InboxDock.Core tests/InboxDock.Core.Tests
git commit -m "feat: validate vault and persist settings"
```

### Task 3: Implement safe naming and Markdown generation with TDD

**Files:**
- Create: `src/InboxDock.Core/Capture/CaptureModels.cs`
- Create: `src/InboxDock.Core/Capture/SafeName.cs`
- Create: `src/InboxDock.Core/Markdown/InboxMarkdown.cs`
- Create: `src/InboxDock.Core/Markdown/DailyMarkdown.cs`
- Create: `tests/InboxDock.Core.Tests/Capture/SafeNameTests.cs`
- Create: `tests/InboxDock.Core.Tests/Markdown/InboxMarkdownTests.cs`
- Create: `tests/InboxDock.Core.Tests/Markdown/DailyMarkdownTests.cs`

- [ ] **Step 1: Write failing naming and Markdown tests**

Cover Chinese titles, invalid filename characters, blank text fallback, suffix collisions, URL Markdown, image embeds, non-image wiki links, Daily template replacement, hidden capture markers, and removal of one exact Daily record.

```csharp
[Theory]
[InlineData("理解 Java 接口", "理解 Java 接口")]
[InlineData("A/B:C*D?", "A B C D")]
public void FromText_SanitizesWithoutRemovingChinese(string input, string expected)
    => Assert.Equal(expected, SafeName.FromText(input));
```

- [ ] **Step 2: Verify tests fail**

Run: `dotnet test tests/InboxDock.Core.Tests --filter "FullyQualifiedName~SafeName|FullyQualifiedName~Markdown"`

Expected: compile failure for missing types.

- [ ] **Step 3: Implement deterministic pure functions**

`SafeName` trims whitespace, replaces invalid characters with spaces, collapses whitespace, caps generated titles at 40 text elements, and falls back to `快速记录`. `InboxMarkdown` emits the approved YAML and content sections. `DailyMarkdown` creates a note from template, appends under `## InboxDock 快速记录`, and removes only a list line with the matching GUID marker.

- [ ] **Step 4: Run focused and full tests, then commit**

```powershell
dotnet test InboxDock.slnx
git add src/InboxDock.Core tests/InboxDock.Core.Tests
git commit -m "feat: generate inbox and daily markdown"
```

### Task 4: Implement Inbox text, URL, and file capture transactions

**Files:**
- Create: `src/InboxDock.Core/IO/AtomicFile.cs`
- Create: `src/InboxDock.Core/Capture/InboxCaptureService.cs`
- Create: `tests/InboxDock.IntegrationTests/InboxCaptureServiceTests.cs`
- Create: `tests/InboxDock.IntegrationTests/Support/TemporaryVault.cs`

- [ ] **Step 1: Write failing temporary-Vault integration tests**

Tests cover text, URL, single file, multiple files, Chinese paths, image embeds, non-image links, duplicate names, preservation of originals, and rollback when a source file disappears during capture.

```csharp
[Fact]
public async Task CaptureFilesAsync_CopiesFilesAndCreatesOneInboxNote()
{
    await using var vault = await TemporaryVault.CreateAsync();
    var result = await vault.Service.CaptureFilesAsync([vault.CreateSource("报告.pdf", "data")]);
    Assert.Single(result.AttachmentPaths);
    Assert.True(File.Exists(result.InboxNotePath));
    Assert.Contains("[[报告.pdf]]", await File.ReadAllTextAsync(result.InboxNotePath));
}
```

- [ ] **Step 2: Verify integration tests fail**

Run: `dotnet test tests/InboxDock.IntegrationTests --filter "FullyQualifiedName~InboxCapture"`

Expected: compile failure for missing capture service.

- [ ] **Step 3: Implement transactional captures**

`AtomicFile` writes a same-directory temporary file, flushes, and moves without overwrite. `InboxCaptureService` creates collision-safe paths, copies attachments to `.inboxdock-<guid>.tmp`, commits them, then commits one Inbox note. On any failure, it moves operation-created files to the local recovery directory and never touches source files.

- [ ] **Step 4: Run integration and full tests, then commit**

```powershell
dotnet test InboxDock.slnx
git add src/InboxDock.Core tests/InboxDock.IntegrationTests
git commit -m "feat: capture inbox text and files safely"
```

### Task 5: Implement Daily append, history, and safe undo

**Files:**
- Create: `src/InboxDock.Core/Daily/DailyCaptureService.cs`
- Create: `src/InboxDock.Core/History/CaptureHistory.cs`
- Create: `src/InboxDock.Core/History/HistoryStore.cs`
- Create: `src/InboxDock.Core/History/UndoService.cs`
- Create: `tests/InboxDock.IntegrationTests/DailyCaptureServiceTests.cs`
- Create: `tests/InboxDock.IntegrationTests/UndoServiceTests.cs`

- [ ] **Step 1: Write failing Daily and undo integration tests**

Cover template creation, minimal fallback, four categories, appending to an existing section, retry after an external write, bounded 100-item history, Inbox undo to recovery, Daily marker undo, and safe refusal after the target record was edited.

- [ ] **Step 2: Verify tests fail**

Run: `dotnet test tests/InboxDock.IntegrationTests --filter "FullyQualifiedName~Daily|FullyQualifiedName~Undo"`

Expected: compile failure for missing services.

- [ ] **Step 3: Implement conflict-aware Daily updates**

`DailyCaptureService` rereads and hashes the note before each attempt, produces the next content with `DailyMarkdown`, verifies unchanged source metadata, and atomically replaces it. Retry delays are 100, 250, and 500 ms. New notes use the configured template or approved minimal fallback.

- [ ] **Step 4: Implement bounded history and undo**

`HistoryStore` uses atomic JSON persistence under `%LocalAppData%\InboxDock`. `UndoService` moves InboxDock-created Vault files to `Recovery\<captureId>` and removes only the exact Daily marker. It marks a history entry undone only after all actions succeed.

- [ ] **Step 5: Run all tests and commit**

```powershell
dotnet test InboxDock.slnx
git add src/InboxDock.Core tests
git commit -m "feat: append daily entries and undo captures"
```

### Task 6: Build the WPF shell, theme, and motion

**Files:**
- Modify: `src/InboxDock.App/App.xaml`
- Modify: `src/InboxDock.App/App.xaml.cs`
- Modify: `src/InboxDock.App/MainWindow.xaml`
- Modify: `src/InboxDock.App/MainWindow.xaml.cs`
- Create: `src/InboxDock.App/Themes/Colors.xaml`
- Create: `src/InboxDock.App/Themes/Controls.xaml`
- Create: `src/InboxDock.App/ViewModels/MainViewModel.cs`
- Create: `src/InboxDock.App/Converters/*.cs`

- [ ] **Step 1: Add a view-model smoke test**

Add `tests/InboxDock.Core.Tests/ViewModels/MainViewModelContractTests.cs` or an app-specific test project that asserts stable default state: Capture tab selected, no busy state, collapsed size 56, expanded size 340 by 480, and commands disabled when Vault is invalid.

- [ ] **Step 2: Implement the stable two-state shell**

Create a borderless, transparent WPF window with maximum 8 px corners, fixed collapsed and expanded dimensions, Capture and Today tabs, accessible tooltips, stable status footer, and Lucide icons for pin, collapse, settings, undo, folder, and external open.

- [ ] **Step 3: Implement light/dark resources and restrained motion**

Use white/charcoal surfaces, green success, amber warning, no gradients, and no nested cards. Implement 160 ms expand/collapse, drag-enter state, and 600 ms success feedback. Respect Windows animation settings by replacing storyboards with immediate state changes.

- [ ] **Step 4: Build and launch the shell**

Run: `dotnet run --project src/InboxDock.App`

Expected: a nonblank 56 px launcher expands to a 340 by 480 panel with no clipping at 100% scaling.

- [ ] **Step 5: Commit UI shell**

```powershell
git add src/InboxDock.App tests
git commit -m "feat: add polished desktop capture shell"
```

### Task 7: Wire capture, settings, tray, startup, and Obsidian open actions

**Files:**
- Modify: `src/InboxDock.App/ViewModels/MainViewModel.cs`
- Modify: `src/InboxDock.App/MainWindow.xaml.cs`
- Create: `src/InboxDock.App/Services/TrayService.cs`
- Create: `src/InboxDock.App/Services/StartupService.cs`
- Create: `src/InboxDock.App/Services/ObsidianLauncher.cs`
- Create: `src/InboxDock.App/Views/SettingsWindow.xaml`
- Create: `src/InboxDock.App/Views/SettingsWindow.xaml.cs`
- Create: `tests/InboxDock.Core.Tests/Services/ObsidianLauncherTests.cs`

- [ ] **Step 1: Write failing URI and state tests**

Assert percent-encoded `obsidian://open?vault=...&file=...`, Explorer fallback arguments, Vault-invalid disabled commands, busy-state reentrancy protection, and reset after capture failure.

- [ ] **Step 2: Implement drop and capture commands**

The window accepts `FileDrop`; text/URL and Daily commands validate inputs, call Core asynchronously, update status/history, clear input only on success, and expose cancellation. Dragging never changes the window dimensions.

- [ ] **Step 3: Implement tray and window persistence**

Use `NotifyIcon` with show/hide, Capture, Today, Settings, and Quit commands. Closing hides unless Quit was chosen. Save screen-aware coordinates and clamp restored coordinates to a visible monitor work area.

- [ ] **Step 4: Implement settings and startup**

Settings validate `.obsidian`, preview canonical destinations, and persist only valid values. Startup writes or removes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\InboxDock` and never changes unrelated entries.

- [ ] **Step 5: Run full tests and manual workflow, then commit**

```powershell
dotnet test InboxDock.slnx
git add src tests
git commit -m "feat: connect desktop workflows and tray"
```

### Task 8: Add open-source documentation, CI, assets, and release packaging

**Files:**
- Create: `README.md`
- Create: `LICENSE`
- Create: `CONTRIBUTING.md`
- Create: `.github/workflows/ci.yml`
- Create: `.github/workflows/release.yml`
- Create: `assets/inboxdock.ico`
- Create: `assets/screenshots/inboxdock-expanded.png`

- [ ] **Step 1: Add MIT license and user-facing documentation**

README must explain privacy, exact Vault writes, first run, capture, Daily, undo, troubleshooting, build, test, and publish commands. It must state Windows-only and no direct Codex/API connection.

- [ ] **Step 2: Add CI and tagged release workflows**

CI uses Windows, restores, builds Release, and runs all tests. Release triggers on `v*` tags, publishes self-contained `win-x64`, zips output, and uploads the ZIP artifact without committing binaries.

- [ ] **Step 3: Capture and inspect screenshots**

Launch the app against a disposable Vault. Capture collapsed and expanded states at 100% and 150% scaling. Confirm nonblank rendering, correct framing, no overlapping controls, visible focus, and readable longest Chinese labels.

- [ ] **Step 4: Publish and inspect the portable build**

```powershell
dotnet publish src/InboxDock.App -c Release -r win-x64 --self-contained true
Get-ChildItem src/InboxDock.App/bin/Release/net10.0-windows/win-x64/publish
```

Expected: a launchable `InboxDock.exe`; no source or test files in publish output.

- [ ] **Step 5: Commit release materials**

```powershell
git add README.md LICENSE CONTRIBUTING.md .github assets src/InboxDock.App
git commit -m "docs: prepare InboxDock for GitHub release"
```

### Task 9: Final verification

**Files:**
- Verify all source, tests, docs, workflows, and release artifacts

- [ ] **Step 1: Run clean build and all automated tests**

```powershell
dotnet clean InboxDock.slnx
dotnet restore InboxDock.slnx
dotnet build InboxDock.slnx -c Release --no-restore
dotnet test InboxDock.slnx -c Release --no-build
```

Expected: zero warnings, zero errors, all tests passed.

- [ ] **Step 2: Run end-to-end disposable-Vault checks**

With Obsidian closed, verify text, URL, Chinese file, duplicate file, multiple files, four Daily categories, latest-operation undo, invalid Vault, tray restore, and application restart. Confirm original dropped files remain unchanged.

- [ ] **Step 3: Verify repository and package hygiene**

Confirm no API keys, personal Vault content, local absolute path in defaults, `bin`, `obj`, or local settings are tracked. Validate workflow YAML and list the portable ZIP contents.

- [ ] **Step 4: Commit any verification-only fixes and tag readiness**

```powershell
git status --short
git log --oneline --decorate -10
```

Expected: clean worktree on `main`, documented commands pass, and no tag is created or pushed without the user's explicit request.
