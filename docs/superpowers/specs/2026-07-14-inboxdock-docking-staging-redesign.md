# InboxDock Docking and Staging Redesign

## Summary

This redesign turns InboxDock into a smaller edge-docked desktop bucket with a persistent staging tray. The collapsed block can be clicked anywhere to expand or dragged anywhere to move. After dragging, it snaps to the nearest edge of the current Windows display. Expansion is anchored to that edge, so the panel always grows inward rather than always using its upper-left corner.

The Capture tab no longer uses a large plain text box plus a manual submit button. Files, links, and text become pending material cards in one white staging tray. New materials show an inline confirmation sheet. Confirmed items are captured into the Obsidian Vault; deferred items remain in the tray across application restarts.

## Goals

- Make the entire collapsed block clickable to expand.
- Make the entire collapsed block draggable without hidden button hit targets.
- Snap to the nearest left, right, top, or bottom edge after dragging.
- Expand inward while keeping the snapped edge fixed.
- Reduce the expanded footprint from 340 by 480 to approximately 304 by 408 device-independent pixels.
- Apply real rounded clipping to the window and consistent rounded control templates.
- Replace the plain Capture text box with a persistent material staging tray.
- Ask for confirmation automatically after files are dropped, links are pasted, or text is submitted with Ctrl+Enter.
- Keep deferred materials available after restart without touching the original source files.

## Non-Goals

- Changing the Daily file format or Codex workflow.
- Automatically classifying staged materials with AI.
- Uploading staged materials to a network service.
- Supporting drag-and-drop folders in this release.
- Adding cloud sync between InboxDock installations.
- Redesigning the settings workflow beyond what is needed for window and staging behavior.

## Root Causes in the Current Version

The current collapsed state only animates the window to 56 by 56 while retaining the expanded header grid. Three fixed 44-pixel button columns still occupy the collapsed hit-test area, so the visible block is effectively covered by buttons and cannot act as a reliable drag surface.

The current animation changes only `Width` and `Height`. It does not animate `Left` and `Top`, does not record a dock edge, and does not calculate a target rectangle from the monitor work area. Expansion therefore uses the current upper-left position regardless of the edge where the user placed the window.

The outer `Border` has a corner radius, but its child header and footer backgrounds are not clipped to that radius. Standard WPF templates are still used for text boxes, combo boxes, and most buttons, which leaves visible square corners.

The large white Capture box is a text editor with a separate submit button. It has no pending-item model, no link paste behavior, and no way to retain canceled file drops.

## Window States

### Collapsed

- Size: 52 by 52.
- One dedicated visual tree, separate from the expanded header and body.
- Entire surface is a drag and click target.
- The center shows the InboxDock icon; a subtle grip or edge marker indicates that it can move.
- No pin, collapse, or settings buttons exist in the collapsed visual tree.

Mouse handling distinguishes a click from a drag:

- Movement below 4 device-independent pixels is a click and expands the window.
- Movement at or above 4 pixels is a drag.
- After a drag ends, the window snaps to the nearest edge and remains collapsed.

### Expanded

- Size: 304 by 408.
- The existing Capture and Today tabs remain.
- Header actions remain pin, collapse, and settings.
- Dragging the expanded header also snaps the window when released.

## Docking Model

Introduce a pure, testable geometry model:

```text
DockEdge: Left | Right | Top | Bottom
WindowRect: Left, Top, Width, Height
WorkAreaRect: Left, Top, Width, Height
```

`WindowDockCalculator` receives the current window rectangle, monitor work area, target size, and an 8-pixel edge margin.

The nearest edge is selected by the smallest absolute distance between the current window boundary and each work-area boundary. Only one edge is selected, including near corners. The position along the perpendicular axis remains as close as possible to the user's chosen location and is clamped inside the monitor work area.

Snapped collapsed positions:

- Left: `Left = workArea.Left + margin`
- Right: `Left = workArea.Right - collapsedWidth - margin`
- Top: `Top = workArea.Top + margin`
- Bottom: `Top = workArea.Bottom - collapsedHeight - margin`

Expanded positions preserve the selected boundary:

- Left grows right.
- Right moves `Left` left while preserving the right margin.
- Top grows down.
- Bottom moves `Top` up while preserving the bottom margin.

The current monitor is resolved from the window center after the drag. WinForms screen coordinates are converted from device pixels to WPF device-independent pixels using the current monitor DPI scale.

## Window Animation

One 180-millisecond animation changes `Left`, `Top`, `Width`, and `Height` together. The animation uses an ease-out curve with no overshoot. At completion, animated dependency properties are cleared and final base values are assigned, preventing stale animation values from interfering with later dragging.

Edge snapping uses a separate 140-millisecond position-only animation.

The application stores the current dock edge and snapped position. Restoring a saved window position clamps it to an available monitor, so disconnecting a display cannot leave InboxDock off-screen.

## Rounded Visual System

- Window shell radius: 14.
- Drop target, material tray, text editor, and combo box radius: 10.
- Buttons and tab segments: 8.
- Material cards: 9.

The shell applies a rounded rectangle clip that updates on size changes. Header, body, footer, shadows, and hit testing remain inside this clip.

Custom WPF control templates replace the square default chrome for text boxes, combo boxes, primary actions, icon buttons, and tab items. The palette remains paper white, charcoal, muted gray, and restrained green. The shell uses one soft shadow; nested cards do not add additional shadows.

## Capture Tab Layout

The Capture tab is composed of:

1. A compact file and paste target at the top.
2. A white staging tray in the center.
3. A compact text draft editor integrated at the bottom of the tray.
4. An inline confirmation sheet that appears over the lower tray area when an item awaits a decision.

The old full-width `收进 Inbox` button is removed.

### Empty State

The tray displays a short message explaining that deferred materials will remain here. The text editor shows `在这里写文字，Ctrl+Enter 进入确认`.

### Material Cards

Each pending card contains:

- type icon
- title or filename
- type and size summary
- status: `等待确认`, `待处理`, `正在收集`, `收集失败`
- compact actions for retry/confirm, edit when applicable, and remove

Multiple files dropped in one operation form one grouped card and receive one confirmation.

## Material Input Rules

### Files

Dropping one or more files immediately creates one staged material group. Directories are rejected with a clear message.

### Links

Pasting text that parses as an absolute `http` or `https` URI immediately creates a link card and opens confirmation. Manually typed links follow the text rule and use Ctrl+Enter.

### Text

Ordinary text is written in the draft editor. Ctrl+Enter creates a text card and opens confirmation. Enter alone inserts a new line.

The draft is autosaved locally so closing the application does not lose unfinished text.

## Staging Storage

Staging data is stored under:

```text
%LocalAppData%\InboxDock\Staging
  staging.json
  files\<material-id>\...
```

`staging.json` contains only local pending-item metadata. Each file is copied to its material directory before the card is committed to the list. The source file is never moved or deleted.

Staging writes are atomic:

1. Copy files to temporary names in the staging material directory.
2. Rename completed files.
3. Atomically update `staging.json`.
4. Add the card to the UI.

If any file copy fails, the group is not added and operation-created temporary files are cleaned up.

Staged materials are restored when the application starts. A missing staged file marks its card as failed instead of silently discarding the item.

## Inline Confirmation

The application does not use a Windows message box. A non-blocking sheet slides up inside the tray and shows a concise summary with two actions:

- `收进 Inbox`
- `先暂存`

Confirming calls the existing Inbox capture service using staged file copies or staged text/link content. The card moves to `正在收集`, displays a brief success check, then is removed from staging only after the Vault operation succeeds.

Deferring closes the sheet and changes the card status to `待处理`. Selecting a deferred card opens the confirmation sheet again.

Removing a deferred card requires a second confirmation because it deletes the InboxDock-owned staged copy. It never affects the original source file.

## Motion

- Collapsed/expanded window: 180 ms inward-anchored resize and move.
- Edge snap: 140 ms.
- Card entry: 170 ms fade and 8-pixel upward movement.
- Confirmation sheet: 150 ms upward reveal.
- Successful capture: 450 ms green check followed by card removal.
- Deferred status: 140 ms transition to a muted `待处理` badge.

Motion does not use bounce, elastic overshoot, pulsing, or layout-changing placeholders. Windows reduced-motion settings disable these transitions.

## Data Model

Add a persistent `StagedMaterial` model with:

- ID
- material kind: files, link, text
- display title
- created time
- status
- staged file metadata or textual content
- last error

Add services with clear responsibilities:

- `StagingStore`: atomic JSON persistence and draft persistence.
- `FileStagingService`: copy and validate dropped files.
- `StagedCaptureService`: convert a staged material into an existing Inbox capture operation and remove it only after success.
- `WindowDockCalculator`: pure edge and anchor calculations.

The WPF view model owns selection and confirmation-sheet state but does not perform direct filesystem writes.

## Error Handling

- Invalid or missing staged files remain visible with a retryable error.
- A failed Vault capture keeps the staged item and displays the actual message.
- Insufficient staging disk space creates no card and leaves sources untouched.
- Directories are rejected before staging.
- If the selected Vault becomes invalid, staged materials remain available and confirmation is disabled until the Vault is fixed.
- Monitor changes clamp the window to an available work area.

## Testing

Unit tests cover:

- nearest-edge selection for all four edges
- inward expansion rectangle for all four edges
- perpendicular-axis clamping
- click-versus-drag threshold
- URL recognition
- staged item JSON round trip
- draft persistence

Integration tests cover:

- staging one file and multiple files
- preserving originals
- restoring pending materials after restart
- deferred materials remaining in staging
- confirmed materials entering Inbox and leaving staging
- failed captures remaining available
- Chinese paths and duplicate filenames

Manual desktop checks cover:

- click anywhere on collapsed block to expand
- drag collapsed and expanded states to all four edges
- correct inward expansion from every edge
- 304 by 408 expanded footprint
- rounded clipping at every corner
- file, pasted link, and Ctrl+Enter text confirmation
- defer and retry animations
- 100%, 125%, and 150% Windows scaling

## Acceptance Criteria

- The collapsed block always distinguishes click from drag correctly.
- All four screen edges snap and expand inward.
- The expanded window occupies no more than approximately 304 by 408 DIPs.
- No square child background escapes the rounded shell.
- Files, pasted links, and Ctrl+Enter text all create staged cards and automatic inline confirmation.
- Deferred cards survive application restart.
- Confirmed cards create correct Inbox content and are removed only after success.
- Original files remain unchanged.
- Existing Daily behavior and all current Core tests remain valid.
