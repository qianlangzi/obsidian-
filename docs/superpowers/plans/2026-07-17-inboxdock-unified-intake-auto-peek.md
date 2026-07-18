# InboxDock Unified Intake and Auto Peek Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make InboxDock accept files and clipboard images across the collection surface, support shared file notes and one-confirm capture, fix the Today selector, and replace manual collapse with a low-memory 10-second auto-peek edge animation.

**Architecture:** Keep filesystem and Markdown behavior in `InboxDock.Core`; introduce pure window state/geometry types that can be unit tested without WPF. Keep clipboard access and animations in the WPF app, but route staged content through `MaterialStagingService` and user-idle decisions through a small controller. Update the existing window instead of performing a broad MVVM rewrite.

**Tech Stack:** .NET 10, C#, WPF, CommunityToolkit.Mvvm, xUnit, System.Text.Json.

---

## File map

- Modify `src/InboxDock.Core/Staging/StagedMaterial.cs`: persist optional file-group notes.
- Modify `src/InboxDock.Core/Staging/MaterialStagingService.cs`: update notes and update snapshots without reloading JSON after staging.
- Modify `src/InboxDock.Core/Staging/FileStagingService.cs`: accept an existing snapshot and return the updated material without a second load.
- Modify `src/InboxDock.Core/Capture/InboxCaptureService.cs`: pass file notes into Markdown generation.
- Modify `src/InboxDock.Core/Markdown/InboxMarkdown.cs`: render optional notes before attachments.
- Create `src/InboxDock.Core/Windowing/AutoPeekController.cs`: pure inactivity/pause state machine.
- Modify `src/InboxDock.Core/Windowing/WindowDockCalculator.cs`: calculate edge handle rectangles.
- Create `src/InboxDock.App/Clipboard/ClipboardMaterialReader.cs`: classify clipboard file/image/text payloads.
- Modify `src/InboxDock.App/ViewModels/MainViewModel.cs`: stage clipboard images, edit notes, minimize collection churn, expose busy/confirmation pause state.
- Modify `src/InboxDock.App/MainWindow.xaml`: remove the fixed drop block, add full-surface drop feedback, note editor, virtualized list, correct Today selection display, and arrow handle.
- Modify `src/InboxDock.App/MainWindow.xaml.cs`: whole-surface drop/paste, activity tracking, auto-peek animation, compatibility window state, and resource disposal.
- Modify `src/InboxDock.App/App.xaml`: editor and ComboBox template fixes.
- Add or modify tests under `tests/InboxDock.Core.Tests` and `tests/InboxDock.IntegrationTests` for every core behavior.

### Task 1: Persist file-group notes and render them in Inbox Markdown

**Files:**
- Modify: `src/InboxDock.Core/Staging/StagedMaterial.cs`
- Modify: `src/InboxDock.Core/Markdown/InboxMarkdown.cs`
- Modify: `src/InboxDock.Core/Capture/InboxCaptureService.cs`
- Modify: `src/InboxDock.Core/Staging/StagedCaptureService.cs`
- Modify: `tests/InboxDock.Core.Tests/Staging/StagingStoreTests.cs`
- Modify: `tests/InboxDock.Core.Tests/Markdown/InboxMarkdownTests.cs`
- Modify: `tests/InboxDock.IntegrationTests/StagedCaptureServiceTests.cs`

- [ ] **Step 1: Write failing persistence and Markdown tests**

Add a file material with `Note: "先阅读第二章"` to `StagingStoreTests.SaveAndLoadAsync_RoundTripsCardsAndUnicodeDraft` and assert `Assert.Equal(expected.Note, actual.Note)`.

Add this test to `InboxMarkdownTests`:

```csharp
[Fact]
public void ForFiles_WithNote_WritesNoteBeforeAttachments()
{
    var files = new[] { new CapturedAttachment("报告.pdf", "05 Resources/Attachments/报告.pdf", 42) };

    var markdown = InboxMarkdown.ForFiles(
        "材料 1 项", files, Guid.NewGuid(), DateTimeOffset.Now, "先阅读第二章");

    var normalized = markdown.Replace("\r\n", "\n");
    Assert.Contains("## 备注\n\n先阅读第二章\n\n## 附件", normalized);
}

[Fact]
public void ForFiles_WithoutNote_DoesNotWriteEmptyNoteHeading()
{
    var files = new[] { new CapturedAttachment("报告.pdf", "05 Resources/Attachments/报告.pdf", 42) };

    var markdown = InboxMarkdown.ForFiles(
        "材料 1 项", files, Guid.NewGuid(), DateTimeOffset.Now, null);

    Assert.DoesNotContain("## 备注", markdown);
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj --filter "StagingStoreTests|InboxMarkdownTests"
```

Expected: compilation failures because `StagedMaterial.Note` and the five-argument `InboxMarkdown.ForFiles` overload do not exist.

- [ ] **Step 3: Add the note model and Markdown implementation**

Change the record tail to:

```csharp
public sealed record StagedMaterial(
    Guid Id,
    StagedMaterialKind Kind,
    string Title,
    DateTimeOffset CreatedAt,
    StagedMaterialStatus Status,
    IReadOnlyList<StagedFile> Files,
    string? Content = null,
    string? LastError = null,
    string? Note = null);
```

Change `InboxMarkdown.ForFiles` to accept `string? note` and append a `## 备注` section only when `note` is not whitespace. Pass `material.Note` from `StagedCaptureService` to `InboxCaptureService.CaptureFilesAsync`, and from there to `InboxMarkdown.ForFiles`.

- [ ] **Step 4: Verify GREEN**

Run the focused test command again. Expected: all selected tests pass.

- [ ] **Step 5: Add an integration assertion for captured notes**

In `ConfirmAsync_FileGroup_CapturesFromStagingThenDeletesOnlyOwnedCopies`, update the staged material note before confirming and assert the generated Inbox note contains it.

- [ ] **Step 6: Run integration tests and commit**

```powershell
dotnet test tests/InboxDock.IntegrationTests/InboxDock.IntegrationTests.csproj --filter StagedCaptureServiceTests
git add src/InboxDock.Core tests/InboxDock.Core.Tests tests/InboxDock.IntegrationTests
git commit -m "feat: persist notes for staged file groups"
```

Expected: tests pass and the commit contains only note persistence/capture behavior.

### Task 2: Remove redundant staging reloads and support staged clipboard PNG files

**Files:**
- Modify: `src/InboxDock.Core/Staging/FileStagingService.cs`
- Modify: `src/InboxDock.Core/Staging/MaterialStagingService.cs`
- Modify: `tests/InboxDock.IntegrationTests/MaterialStagingServiceTests.cs`

- [ ] **Step 1: Write failing tests for in-memory snapshot updates and owned PNG staging**

Add tests that stage two groups sequentially using one loaded `MaterialStagingService`, assert both items remain in `Snapshot`, and stage a temporary `.png` file with a clipboard-style name while preserving its bytes.

```csharp
[Fact]
public async Task StageFilesAsync_SequentialGroups_UpdateCurrentSnapshot()
{
    var first = await CreateFileAsync("first.txt", "one");
    var second = await CreateFileAsync("second.txt", "two");
    var service = CreateService();
    await service.LoadAsync();

    await service.StageFilesAsync([first]);
    await service.StageFilesAsync([second]);

    Assert.Equal(2, service.Snapshot.Items.Count);
}
```

- [ ] **Step 2: Run the focused integration test and verify behavior**

Run:

```powershell
dotnet test tests/InboxDock.IntegrationTests/InboxDock.IntegrationTests.csproj --filter MaterialStagingServiceTests
```

Expected: existing tests pass; the new sequential test captures the required behavior before refactoring. Record this as a characterization test, then make the performance change without changing output.

- [ ] **Step 3: Refactor staging to avoid reloading the JSON just written**

Change `FileStagingService.StageFilesAsync` to receive the current `StagingSnapshot` and return `(StagedMaterial Material, StagingSnapshot Snapshot)`. It must save `[...snapshot.Items, material]` exactly once. In `MaterialStagingService.StageFilesAsync`, hold `gate`, call the file service with `Snapshot`, assign the returned snapshot, and return the material. Do not call `LoadAsync` from inside `StageFilesAsync`.

- [ ] **Step 4: Run all staging tests and commit**

```powershell
dotnet test tests/InboxDock.IntegrationTests/InboxDock.IntegrationTests.csproj --filter "MaterialStagingServiceTests|StagedCaptureServiceTests"
git add src/InboxDock.Core/Staging tests/InboxDock.IntegrationTests/MaterialStagingServiceTests.cs
git commit -m "perf: update staging snapshot without reload"
```

Expected: all selected tests pass.

### Task 3: Implement auto-peek state and handle geometry with pure tests

**Files:**
- Create: `src/InboxDock.Core/Windowing/AutoPeekController.cs`
- Modify: `src/InboxDock.Core/Windowing/WindowDockCalculator.cs`
- Create: `tests/InboxDock.Core.Tests/Windowing/AutoPeekControllerTests.cs`
- Modify: `tests/InboxDock.Core.Tests/Windowing/WindowDockCalculatorTests.cs`

- [ ] **Step 1: Write failing state-machine tests**

Create tests for a 10-second due time, activity reset, pause/resume, and peeking/expanded transitions:

```csharp
[Fact]
public void ShouldPeek_AfterTenIdleSeconds_ReturnsTrue()
{
    var now = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);

    Assert.False(controller.ShouldPeek(now.AddSeconds(9)));
    Assert.True(controller.ShouldPeek(now.AddSeconds(10)));
}

[Fact]
public void Resume_StartsAFreshIdleWindow()
{
    var now = DateTimeOffset.UtcNow;
    var controller = new AutoPeekController(TimeSpan.FromSeconds(10), now);
    controller.Pause();
    controller.Resume(now.AddMinutes(1));

    Assert.False(controller.ShouldPeek(now.AddMinutes(1).AddSeconds(9)));
    Assert.True(controller.ShouldPeek(now.AddMinutes(1).AddSeconds(10)));
}
```

- [ ] **Step 2: Write failing handle-rectangle tests**

Add a theory asserting a `20 x 46` left/right handle and `46 x 20` top/bottom handle, clamped along the work area and placed on the corresponding edge.

- [ ] **Step 3: Run tests and verify RED**

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj --filter "AutoPeekControllerTests|WindowDockCalculatorTests"
```

Expected: compilation failure because the controller and `PeekHandleRect` do not exist.

- [ ] **Step 4: Implement minimal pure types**

Implement `AutoPeekController` with `RecordActivity(now)`, `Pause()`, `Resume(now)`, `SetPeeking(bool, now)`, `IsPaused`, `IsPeeking`, and `ShouldPeek(now)`. Implement `WindowDockCalculator.PeekHandleRect(edge, current, workArea, longSide, shortSide)` using existing clamping rules.

- [ ] **Step 5: Verify tests and commit**

```powershell
dotnet test tests/InboxDock.Core.Tests/InboxDock.Core.Tests.csproj --filter "AutoPeekControllerTests|WindowDockCalculatorTests"
git add src/InboxDock.Core/Windowing tests/InboxDock.Core.Tests/Windowing
git commit -m "feat: model automatic edge peeking"
```

Expected: all selected tests pass.

### Task 4: Add clipboard classification and PNG encoding in the WPF app

**Files:**
- Create: `src/InboxDock.App/Clipboard/ClipboardMaterialReader.cs`
- Modify: `src/InboxDock.App/ViewModels/MainViewModel.cs`
- Modify: `src/InboxDock.App/MainWindow.xaml.cs`

- [ ] **Step 1: Define clipboard result types and a deterministic classifier**

Create result records for `Files`, `Image`, `Link`, `Text`, and `Empty`. Keep `Classify(files, hasImage, text)` as a pure internal method so priority can be tested later without opening the system clipboard. `Read()` obtains a WPF `IDataObject`, catches `COMException`, and returns the highest-priority result.

- [ ] **Step 2: Add image encoding and staging flow**

Add `MainViewModel.StageClipboardImageAsync(BitmapSource image)`. Freeze or clone the bitmap on the UI thread, encode it with `PngBitmapEncoder` into a temporary file under `%LocalAppData%\InboxDock\Clipboard`, call `StageFilesAsync([temporary])`, and delete the temporary source in `finally` because the staging service owns its copied version.

- [ ] **Step 3: Replace text-only Ctrl+V handling**

In `OnDraftPreviewKeyDown`, call `ClipboardMaterialReader.Read()` and handle files, image, link, or plain text. Plain text uses the existing selection replacement logic. Mark the event handled only when InboxDock has successfully classified the payload.

- [ ] **Step 4: Build the app and manually exercise classification errors**

```powershell
dotnet build src/InboxDock.App/InboxDock.App.csproj
```

Expected: build succeeds. Clipboard contention is reported through `StatusText` and does not terminate the app.

- [ ] **Step 5: Commit**

```powershell
git add src/InboxDock.App/Clipboard src/InboxDock.App/ViewModels/MainViewModel.cs src/InboxDock.App/MainWindow.xaml.cs
git commit -m "feat: accept files and images from clipboard"
```

### Task 5: Redesign the collection surface, note editor, and Today selector

**Files:**
- Modify: `src/InboxDock.App/MainWindow.xaml`
- Modify: `src/InboxDock.App/App.xaml`
- Modify: `src/InboxDock.App/MainWindow.xaml.cs`
- Modify: `src/InboxDock.App/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Expose editable selected note state**

Add `SelectedNote` to the view model. When selecting a file material set it from `material.Note`; debounce changes through `MaterialStagingService.UpdateAsync(id, item => item with { Note = normalized })`. `ConfirmSelectedAsync` must await a `FlushSelectedNoteAsync()` method before capture.

- [ ] **Step 2: Replace the fixed drop zone with full-surface drag handlers**

Remove the 47-pixel `DropZone` row. Put `AllowDrop`, `DragEnter`, `DragOver`, `DragLeave`, and `Drop` on the collection tab root grid. Add a `Panel.ZIndex` overlay border bound to drag state. Wire its events to the existing multi-file `StageFilesAsync` call.

- [ ] **Step 3: Make the card list virtualized**

Replace `ScrollViewer + ItemsControl` with a `ListBox` whose `ItemsPanel` is `VirtualizingStackPanel`, `VirtualizingPanel.IsVirtualizing=True`, `VirtualizingPanel.VirtualizationMode=Recycling`, and whose item container removes selection chrome while retaining the current card template.

- [ ] **Step 4: Fix the draft editor layout**

Move `Ctrl + Enter` into a dedicated row below the editor. Set `MinHeight=58`, `MaxHeight=112`, `VerticalContentAlignment=Top`, a stable `LineHeight`, and scroll only beyond maximum height. Ensure the hint is outside `PART_ContentHost` and never overlaps text.

- [ ] **Step 5: Add the optional file note editor to confirmation**

Show the note editor only when `SelectedMaterial.Kind == Files`. Bind it to `SelectedNote`, label it `备注（可选）`, and increase the confirmation sheet layout without obscuring the buttons.

- [ ] **Step 6: Fix Today selection rendering**

Replace the current selected content binding in the ComboBox template with a presenter bound to `SelectedItem.Label`, while dropdown items bind `Label`. Verify the primary button template forwards `Foreground` so button text remains white.

- [ ] **Step 7: Build and commit**

```powershell
dotnet build src/InboxDock.App/InboxDock.App.csproj
git add src/InboxDock.App/MainWindow.xaml src/InboxDock.App/App.xaml src/InboxDock.App/MainWindow.xaml.cs src/InboxDock.App/ViewModels/MainViewModel.cs src/InboxDock.Core/Staging/MaterialStagingService.cs
git commit -m "feat: unify capture surface and file notes"
```

Expected: build succeeds; the fixed top drop block no longer exists in XAML.

### Task 6: Replace collapsed mode with animated automatic edge peeking

**Files:**
- Modify: `src/InboxDock.App/MainWindow.xaml`
- Modify: `src/InboxDock.App/MainWindow.xaml.cs`

- [ ] **Step 1: Add the arrow-handle visual tree**

Replace `CollapsedShell` with `PeekShell`, containing a small rounded handle and a Lucide chevron whose direction follows `DockEdge`. Keep the handle as the only hit-testable content in peeking state; mouse enter calls `ExpandFromPeekAsync`.

- [ ] **Step 2: Add a single idle timer and activity hooks**

Create one `DispatcherTimer` ticking at 250ms and one `AutoPeekController` configured for 10 seconds. Register window-level preview mouse, wheel, key, focus, drag, and button events to call `RecordActivity`. Pause while `vm.IsBusy`, confirmation is visible, a modal folder dialog is open, or an IME composition is active; resume with a new full interval.

- [ ] **Step 3: Implement peeking and expansion animations**

Replace `collapsed` with an enum-backed display state. `PeekAsync` computes `PeekHandleRect`, fades/scales the expanded shell, animates window geometry for about 260ms, then shows the handle. `ExpandFromPeekAsync` computes the inward expanded rectangle, shows the shell, and animates geometry/opacity/scale back to one. Reduced-motion mode assigns final values directly.

- [ ] **Step 4: Migrate saved window state**

Save `Peeking` and `Topmost`. Keep the legacy `Collapsed` property nullable or use a custom normalization method so old state files restore as peeking. Clamp restored geometry through `WindowDockCalculator`.

- [ ] **Step 5: Dispose long-lived resources**

On real application exit, stop the timer, cancel pending view-model saves, detach window handlers, dispose `NotifyIcon` and its `ContextMenuStrip`, and clear animation clocks. Do not force garbage collection.

- [ ] **Step 6: Build, run, and commit**

```powershell
dotnet build InboxDock.sln -c Release
dotnet run --project src/InboxDock.App/InboxDock.App.csproj
```

Manual expected behavior: after 10 idle seconds the window leaves only an edge arrow; mouse entry expands it; typing, dragging, confirmations, and capture keep it open.

```powershell
git add src/InboxDock.App/MainWindow.xaml src/InboxDock.App/MainWindow.xaml.cs
git commit -m "feat: auto peek InboxDock at screen edges"
```

### Task 7: Full regression, memory smoke test, and documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/plans/2026-07-17-inboxdock-unified-intake-auto-peek.md`

- [ ] **Step 1: Run the complete automated suite**

```powershell
dotnet test InboxDock.sln -c Release
```

Expected: zero failed tests.

- [ ] **Step 2: Run build and publish verification**

```powershell
dotnet build InboxDock.sln -c Release
dotnet publish src/InboxDock.App/InboxDock.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish
```

Expected: both commands exit 0 and `artifacts/publish/InboxDock.exe` exists.

- [ ] **Step 3: Perform UI regression checks**

Check whole-surface drag, one/multiple files, pasted screenshot image, URL paste, ordinary text paste, Ctrl+Enter, file note persistence, Today selector text, four-edge auto peek, active-input pause, reduced motion, and 100/125/150 percent scaling.

- [ ] **Step 4: Perform a memory/handle smoke test**

Run the Release app, record `PrivateMemorySize64`, `WorkingSet64`, and `HandleCount` after warm-up, repeat at least 30 expand/peek cycles, wait two idle intervals, and record again. Fail the check if handles grow on every cycle or private memory continues increasing after warm-up without stabilizing.

- [ ] **Step 5: Update README behavior descriptions**

Document whole-surface file drop, clipboard images, file-group notes, single confirmation for multi-file groups, automatic 10-second edge peeking, and the arrow-hover expansion behavior. Remove instructions for the 52×52 manual collapsed block and top drop target.

- [ ] **Step 6: Mark executed checkboxes and commit final documentation**

```powershell
git add README.md docs/superpowers/plans/2026-07-17-inboxdock-unified-intake-auto-peek.md
git commit -m "docs: explain unified intake and automatic peeking"
git status --short
```

Expected: working tree is clean except intentionally generated ignored artifacts.

## Execution record

Implemented on branch `feature/unified-intake-auto-peek` on 2026-07-18.

- Core and integration suite: 82 passed, 0 failed.
- Release build: succeeded with 0 warnings and 0 errors.
- Self-contained single-file publish: `artifacts/publish/InboxDock.exe`, 181,665,535 bytes.
- Runtime auto-peek smoke check: the test instance rewrote window state to `Peeking: true` at the right screen edge after the idle interval.
- Idle resource sample: private memory 217.38 MB after warm-up and 217.40 MB after continued idle; handles changed from 812 to 809.
- The existing user window state was restored after runtime testing.
- Windows window-capture automation could not enumerate the 20×46 borderless peeking handle, so final visual judgment for animation smoothness and 100/125/150 percent DPI remains a hands-on release check.
