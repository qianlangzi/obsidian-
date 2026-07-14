# InboxDock Docking and Staging Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace InboxDock's clipped collapsed window and manual Capture form with four-edge inward docking, a compact rounded shell, and a persistent material staging tray with inline confirmation.

**Architecture:** Pure window geometry and staging persistence live in `InboxDock.Core` so behavior can be tested without WPF. `MainViewModel` orchestrates staged cards and existing Inbox capture services, while `MainWindow` owns monitor/DPI conversion, drag gestures, anchored animations, and visual state. Files are copied to LocalAppData staging before a card is committed, and are removed from staging only after successful Vault capture.

**Tech Stack:** .NET 10, C#, WPF, CommunityToolkit.Mvvm, MahApps Lucide icons, System.Text.Json, xUnit.

---

### Task 1: Create an isolated branch and verify the current baseline

**Files:**
- Read: `docs/superpowers/specs/2026-07-14-inboxdock-docking-staging-redesign.md`
- Read: `src/InboxDock.App/MainWindow.xaml`
- Read: `src/InboxDock.App/MainWindow.xaml.cs`

- [ ] **Step 1: Create an isolated worktree branch**

Use branch `feature/docking-staging-redesign` under `.worktrees/docking-staging-redesign` after confirming `.worktrees/` is ignored.

- [ ] **Step 2: Run baseline checks**

```powershell
dotnet restore InboxDock.sln
dotnet build InboxDock.sln -c Release --no-restore
dotnet test InboxDock.sln -c Release --no-build
```

Expected: 0 warnings, 0 errors, 24 tests passed.

### Task 2: Implement test-first edge docking geometry

**Files:**
- Create: `src/InboxDock.Core/Windowing/DockEdge.cs`
- Create: `src/InboxDock.Core/Windowing/WindowRect.cs`
- Create: `src/InboxDock.Core/Windowing/WindowDockCalculator.cs`
- Create: `src/InboxDock.Core/Windowing/PointerGesture.cs`
- Create: `tests/InboxDock.Core.Tests/Windowing/WindowDockCalculatorTests.cs`
- Create: `tests/InboxDock.Core.Tests/Windowing/PointerGestureTests.cs`

- [ ] **Step 1: Write failing nearest-edge and anchored-expansion tests**

```csharp
[Theory]
[InlineData(10, 300, DockEdge.Left)]
[InlineData(940, 300, DockEdge.Right)]
[InlineData(400, 10, DockEdge.Top)]
[InlineData(400, 740, DockEdge.Bottom)]
public void NearestEdge_SelectsClosestBoundary(double left, double top, DockEdge expected)
{
    var current = new WindowRect(left, top, 52, 52);
    var workArea = new WindowRect(0, 0, 1000, 800);
    Assert.Equal(expected, WindowDockCalculator.NearestEdge(current, workArea));
}

[Fact]
public void ExpandedRect_FromRightEdge_PreservesRightMargin()
{
    var result = WindowDockCalculator.TargetRect(
        DockEdge.Right,
        new WindowRect(940, 200, 52, 52),
        new WindowRect(0, 0, 1000, 800),
        304,
        408,
        8);
    Assert.Equal(688, result.Left);
    Assert.Equal(984, result.Right);
}

[Theory]
[InlineData(DockEdge.Left, 8, 200, 304, 408)]
[InlineData(DockEdge.Right, 688, 200, 304, 408)]
[InlineData(DockEdge.Top, 400, 8, 304, 408)]
[InlineData(DockEdge.Bottom, 400, 384, 304, 408)]
public void TargetRect_GrowsInwardFromEveryEdge(
    DockEdge edge, double expectedLeft, double expectedTop, double expectedWidth, double expectedHeight)
{
    var collapsed = edge switch
    {
        DockEdge.Left => new WindowRect(8, 200, 52, 52),
        DockEdge.Right => new WindowRect(940, 200, 52, 52),
        DockEdge.Top => new WindowRect(400, 8, 52, 52),
        _ => new WindowRect(400, 740, 52, 52),
    };

    var result = WindowDockCalculator.TargetRect(
        edge, collapsed, new WindowRect(0, 0, 1000, 800), 304, 408, 8);

    Assert.Equal(new WindowRect(expectedLeft, expectedTop, expectedWidth, expectedHeight), result);
}

[Fact]
public void TargetRect_ClampsPerpendicularPositionInsideWorkArea()
{
    var result = WindowDockCalculator.TargetRect(
        DockEdge.Left,
        new WindowRect(8, 780, 52, 52),
        new WindowRect(0, 0, 1000, 800),
        304,
        408,
        8);
    Assert.Equal(384, result.Top);
}
```

- [ ] **Step 2: Write failing click-versus-drag threshold tests**

```csharp
[Theory]
[InlineData(0, 0, 3, 0, true)]
[InlineData(0, 0, 4, 0, false)]
public void IsClick_UsesFourDipThreshold(double x1, double y1, double x2, double y2, bool expected)
    => Assert.Equal(expected, PointerGesture.IsClick(x1, y1, x2, y2, 4));
```

- [ ] **Step 3: Run tests and verify RED**

Run: `dotnet test tests/InboxDock.Core.Tests --filter "FullyQualifiedName~Windowing"`

Expected: compile failure because windowing types do not exist.

- [ ] **Step 4: Implement pure geometry**

Create `DockEdge { Left, Right, Top, Bottom }`. `WindowRect` is a readonly record struct with `Left`, `Top`, `Width`, `Height`, plus computed `Right` and `Bottom`. `NearestEdge(WindowRect current, WindowRect workArea)` compares distances to all four work-area boundaries in enum order for deterministic ties. `TargetRect(DockEdge edge, WindowRect current, WindowRect workArea, double width, double height, double margin)` preserves the chosen edge margin and clamps the perpendicular coordinate between the opposite work-area margins. `PointerGesture.IsClick(double startX, double startY, double endX, double endY, double threshold)` uses Euclidean movement strictly below the threshold.

- [ ] **Step 5: Run full tests and commit**

```powershell
dotnet test InboxDock.sln
git add src/InboxDock.Core/Windowing tests/InboxDock.Core.Tests/Windowing
git commit -m "feat: calculate four-edge window docking"
```

### Task 3: Implement persistent material staging with TDD

**Files:**
- Create: `src/InboxDock.Core/Staging/StagedMaterial.cs`
- Create: `src/InboxDock.Core/Staging/StagingSnapshot.cs`
- Create: `src/InboxDock.Core/Staging/StagingStore.cs`
- Create: `src/InboxDock.Core/Staging/FileStagingService.cs`
- Create: `tests/InboxDock.Core.Tests/Staging/StagingStoreTests.cs`
- Create: `tests/InboxDock.IntegrationTests/MaterialStagingServiceTests.cs`

- [ ] **Step 1: Write failing store round-trip and draft tests**

Create tests named `SaveAndLoadAsync_RoundTripsCardsAndUnicodeDraft`, `LoadAsync_WhenFileDoesNotExist_ReturnsEmptySnapshot`, and `LoadAsync_WhenJsonIsInvalid_ReturnsErrorWithoutChangingSource`. Construct `StagingStore(root.Path)`, save a `StagingSnapshot` containing one card of each kind and draft `"今天想到：材料桶"`, then assert IDs, enum values, file metadata, content, errors, and draft round-trip. For invalid JSON, assert `StagingLoadResult.Snapshot.Items` is empty, `Error` is non-empty, and the original bytes remain unchanged.

- [ ] **Step 2: Write failing file staging tests**

Create `FileStagingServiceTests` with `StageFilesAsync_CopiesOneFileWithoutChangingSource`, `StageFilesAsync_GroupsFilesAndPreservesChineseNames`, `StageFilesAsync_DuplicateNamesReceiveUniqueStagedNames`, `StageFilesAsync_RejectsDirectories`, `StageFilesAsync_WhenOneSourceIsMissing_RollsBackWholeGroup`, and `StageFilesAsync_AfterRestart_RestoresPersistedGroup`. Compare source hashes before and after staging; assert grouped files share one material ID; assert rollback leaves neither metadata nor a material directory.

- [ ] **Step 3: Verify RED**

Run: `dotnet test InboxDock.sln --filter "FullyQualifiedName~Staging"`

Expected: compile failure for missing staging types.

- [ ] **Step 4: Implement models and atomic storage**

Use:

```csharp
public enum StagedMaterialKind { Files, Link, Text }
public enum StagedMaterialStatus { AwaitingConfirmation, Deferred, Capturing, Failed }
public sealed record StagedFile(string OriginalPath, string OriginalName, string StagedPath, long SizeBytes);
public sealed record StagedMaterial(
    Guid Id,
    StagedMaterialKind Kind,
    string Title,
    DateTimeOffset CreatedAt,
    StagedMaterialStatus Status,
    IReadOnlyList<StagedFile> Files,
    string? Content = null,
    string? LastError = null);
public sealed record StagingSnapshot(IReadOnlyList<StagedMaterial> Items, string DraftText);
public sealed record StagingLoadResult(StagingSnapshot Snapshot, string? Error = null);
```

`StagingStore(string rootDirectory)` exposes `LoadAsync`, `SaveAsync`, and `RootDirectory`; its parameterless factory path is `%LocalAppData%\InboxDock\Staging`. It persists `staging.json` through a same-directory temporary file and replaces the destination only after successful serialization. `FileStagingService(StagingStore store, Func<DateTimeOffset>? clock = null)` exposes `StageFilesAsync(IReadOnlyList<string>, CancellationToken)` and copies files into `files\<material-id>` using temporary suffixes, then persists the card. Duplicate base names use `name (2).ext`, `name (3).ext` inside the group.

- [ ] **Step 5: Add URL recognition and text staging**

Add `MaterialStagingService(StagingStore store, FileStagingService files, Func<DateTimeOffset>? clock = null)` with `LoadAsync`, `StageFilesAsync`, `StagePastedLinkAsync`, `StageDraftAsync`, `SaveDraftAsync`, and `SaveSnapshotAsync`. `TryNormalizeHttpUrl(string, out string)` accepts only absolute `http` and `https` URIs. Pasted non-URLs return `null` without creating a card; Ctrl+Enter treats all non-empty input as text, including URL-shaped input, clears the persisted draft after the card is saved, and rejects whitespace-only content.

- [ ] **Step 6: Run full tests and commit**

```powershell
dotnet test InboxDock.sln
git add src/InboxDock.Core/Staging tests
git commit -m "feat: persist the material staging tray"
```

### Task 4: Confirm, defer, remove, and capture staged materials

**Files:**
- Create: `src/InboxDock.Core/Staging/StagedCaptureService.cs`
- Create: `tests/InboxDock.IntegrationTests/StagedCaptureServiceTests.cs`
- Modify: `src/InboxDock.App/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Write failing staged-capture integration tests**

Create integration tests with these exact behaviors:

- deferring changes only the status and keeps staged files
- confirming text and links creates Inbox notes and removes the card
- confirming file groups copies to Vault through `InboxCaptureService`, removes staged copies only after success, and preserves originals
- a failed Vault capture keeps the item with `Failed` status and error
- removing a deferred card deletes only InboxDock staging files

- [ ] **Step 2: Verify RED**

Run: `dotnet test tests/InboxDock.IntegrationTests --filter "FullyQualifiedName~StagedCapture"`

Expected: compile failure for missing `StagedCaptureService`.

- [ ] **Step 3: Implement staged transitions**

`StagedCaptureService(MaterialStagingService staging, InboxCaptureService inbox)` exposes `ConfirmAsync(Guid)`, `DeferAsync(Guid)`, `RemoveAsync(Guid)`, and `RetryAsync(Guid)`. It persists `Capturing` before invoking the existing capture service. Text and link cards call `CaptureTextAsync(Content)`; file cards call `CaptureFilesAsync(Files.Select(file => file.StagedPath))`. Success returns the `CaptureResult`, removes metadata, then deletes only `files\<material-id>`. Failure persists `Failed` with the actual message and rethrows. `DeferAsync` persists `Deferred`. `RemoveAsync` rejects `Capturing`, removes metadata, and deletes only InboxDock-owned staged files.

- [ ] **Step 4: Refactor MainViewModel around cards**

Add observable staged cards, selected material, confirmation-sheet state, draft text, and commands/methods:

```text
InitializeStagingAsync
StageFilesAsync
StagePastedLinkAsync
SubmitDraftAsync
ConfirmSelectedAsync
DeferSelectedAsync
RemoveSelectedAsync
SelectMaterial
```

`MainViewModel` receives optional stores/services through an internal test constructor and creates LocalAppData-backed defaults in the public constructor. `StagedItems` is an `ObservableCollection<StagedMaterial>` populated from the service snapshot. `SelectedMaterial` and `IsConfirmationOpen` change together; awaiting cards open automatically, deferred/failed cards reopen when selected. Draft changes are saved with a 300-millisecond cancellation-based debounce. Existing `DailyText`, `SelectedCategory`, `AppendDailyAsync`, and undo behavior remain unchanged.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test InboxDock.sln
git add src tests
git commit -m "feat: confirm and defer staged materials"
```

### Task 5: Rebuild the WPF window and interactions

**Files:**
- Modify: `src/InboxDock.App/App.xaml`
- Replace: `src/InboxDock.App/MainWindow.xaml`
- Replace: `src/InboxDock.App/MainWindow.xaml.cs`
- Create: `src/InboxDock.App/Windowing/MonitorWorkArea.cs`
- Create: `src/InboxDock.App/Converters/StagingConverters.cs`

- [ ] **Step 1: Create separate collapsed and expanded visual trees**

Collapsed view is 52 by 52 and owns the entire hit surface. Expanded view is 304 by 408. Toggle visibility after the anchored animation completes. The collapsed view contains no header action buttons.

- [ ] **Step 2: Implement click-anywhere and drag-anywhere**

On collapsed mouse down, record pointer and window positions, call `DragMove`, compare movement, then either expand or snap. Expanded header drag always snaps on release.

- [ ] **Step 3: Implement monitor-aware four-edge docking**

Resolve `Forms.Screen` from the window center, convert work-area pixels using `VisualTreeHelper.GetDpi`, call `WindowDockCalculator`, and animate `Left`, `Top`, `Width`, and `Height`. Save the latest dock edge and position.

- [ ] **Step 4: Build the compact rounded shell**

Implement custom rounded templates for text boxes, combo boxes, primary buttons, icon buttons, tabs, and material cards. Update the shell clip on `SizeChanged` with a 14-pixel rounded rectangle geometry. Use one soft shadow and no square background outside the clip.

- [ ] **Step 5: Build the staging tray and inline confirmation sheet**

Capture tab includes compact drop target, material `ItemsControl`, integrated draft editor, empty state, and animated inline sheet. Remove the old manual capture button.

File drop calls `StageFilesAsync`. Preview Ctrl+V detects a valid URL and stages it immediately. Ctrl+Enter stages text. Selecting a deferred card reopens confirmation.

- [ ] **Step 6: Implement restrained motion**

Use 180 ms anchored window animation, 140 ms edge snap, 170 ms card entry, 150 ms confirmation reveal, and 450 ms success state. Disable storyboards when Windows client-area animation is disabled.

- [ ] **Step 7: Build, run, and commit**

```powershell
dotnet build InboxDock.sln -c Debug
dotnet test InboxDock.sln -c Debug --no-build
git add src/InboxDock.App
git commit -m "feat: redesign the docked staging interface"
```

### Task 6: Desktop verification, documentation, and release package

**Files:**
- Modify: `README.md`
- Replace: `assets/screenshots/inboxdock-expanded.jpg`
- Create: `assets/screenshots/inboxdock-collapsed.jpg`

- [ ] **Step 1: Run automated verification**

```powershell
dotnet clean InboxDock.sln -c Release
dotnet restore InboxDock.sln
dotnet build InboxDock.sln -c Release --no-restore
dotnet test InboxDock.sln -c Release --no-build
```

Expected: 0 warnings, 0 errors, all old and new tests passed.

- [ ] **Step 2: Verify all four edges interactively**

At 100% scaling, drag the collapsed block to left, right, top, and bottom. Verify automatic snap, click-anywhere expansion, inward anchored expansion, and no off-screen content. Repeat representative right and bottom tests at 125% or 150% scaling.

- [ ] **Step 3: Verify the material tray**

Against a disposable Vault, test grouped files, pasted link, Ctrl+Enter text, confirm, defer, restart restore, retry, and remove. Confirm originals remain unchanged and deferred items survive restart.

- [ ] **Step 4: Capture screenshots and update README**

Save clean collapsed and expanded screenshots. Document four-edge snapping, the staging tray, confirmation behavior, Ctrl+Enter, LocalAppData staging, and deferred-item persistence.

- [ ] **Step 5: Publish and inspect the portable ZIP**

```powershell
dotnet publish src/InboxDock.App -c Release -r win-x64 --self-contained true -o artifacts/publish
Compress-Archive artifacts/publish/* artifacts/InboxDock-win-x64.zip
```

Verify the ZIP contains only `InboxDock.exe`, launch it, and confirm the startup log reaches `Main window shown`.

- [ ] **Step 6: Commit and prepare integration**

```powershell
git add README.md assets src tests
git commit -m "docs: document docking and material staging"
git status --short
```

Expected: clean feature branch ready for final verification and merge.
