# InboxDock Design

## Summary

InboxDock is a lightweight Windows companion for an Obsidian Vault. It remains available when Obsidian is hidden or fully closed, accepts files, text, and URLs from a compact desktop panel, records captures in the Vault Inbox, and appends short entries to the current Daily note.

The first release targets Windows 10 and Windows 11 on x64. It is local-only, has no account, analytics, cloud service, or AI API dependency, and stores all knowledge in ordinary Markdown and attachment files.

## Why a New Application

The Obsidian community registry and relevant GitHub projects were checked on 2026-07-13. Existing tools cover only parts of the workflow:

- Tray and Quick Tray provide background operation, global shortcuts, and quick notes.
- Pebble edits one configured Markdown file from a tray popup.
- Quick note provides a floating note window but does not currently support Windows fully.
- Always On Top keeps the entire Obsidian window above other windows.
- QuickAdd captures text inside Obsidian.

No maintained solution was found that combines an always-available Windows drop target, external file ingestion, Inbox note creation, and Daily note capture while Obsidian is closed. InboxDock therefore uses a standalone desktop architecture instead of an Obsidian plugin.

## Goals

- Remain usable when Obsidian is completely closed.
- Provide a compact, polished desktop drop target and tray application.
- Capture text, URLs, one file, or multiple files into an Obsidian Vault.
- Copy attachments safely and create a corresponding Inbox Markdown note.
- Append categorized, timestamped entries to today's Daily note.
- Never overwrite existing notes or attachments silently.
- Support undo for the most recent Inbox capture or Daily append.
- Package a portable, self-contained Windows x64 release suitable for GitHub Releases.
- Keep the codebase understandable and testable for open-source contributors.

## Non-Goals for Version 1

- Full Markdown editing or preview.
- Automatic AI classification or direct Codex invocation.
- Cloud sync, accounts, telemetry, or network metadata fetching.
- macOS or Linux packages.
- Mobile support.
- Automatic Inbox processing.
- Replacing Obsidian, Claudian, or the existing Codex workflow.
- Installing or configuring Obsidian plugins.

## Technology Choice

InboxDock will use .NET 10 and WPF.

This computer already has the .NET 10 SDK, while a Tauri implementation would first require a Rust toolchain. WPF provides native Windows file drop, system tray, always-on-top window, startup integration, DPI behavior, and filesystem APIs without shipping another Chromium runtime. Electron was rejected because it would duplicate much of Obsidian's runtime and consume more memory while both applications are resident.

Core technology:

- .NET 10, C#, WPF
- `CommunityToolkit.Mvvm` for view models and commands
- Lucide WPF icons for familiar actions
- Native WPF storyboards for restrained motion
- `System.Windows.Forms.NotifyIcon` for the system tray
- xUnit for unit and integration tests

## Repository Structure

```text
InboxDock/
  src/
    InboxDock.App/          WPF UI, tray, startup, window behavior
    InboxDock.Core/         Vault validation, capture, Daily, history, undo
  tests/
    InboxDock.Core.Tests/   Unit tests for naming and Markdown generation
    InboxDock.IntegrationTests/  Temporary-Vault filesystem workflows
  docs/
    superpowers/            Approved design and implementation plan
  assets/                   Application icon and GitHub screenshots
  README.md
  LICENSE
```

`InboxDock.Core` must not depend on WPF. All filesystem workflows are accessible through small service interfaces so they can be tested against temporary directories.

## User Experience

### Window States

InboxDock has two visible states:

1. Collapsed: a fixed 56 by 56 pixel launcher attached near the right edge of the current screen.
2. Expanded: an approximately 340 by 480 pixel tool panel that opens inward from the saved edge position.

The user can drag the window to another screen or edge. Position and collapsed state are restored on restart. The window does not appear in the taskbar and can be shown or hidden from the tray icon. Always-on-top can be toggled from the header.

### Expanded Layout

The header contains the InboxDock name and icon-only controls for pin, collapse, and settings. Every icon has a tooltip and accessible name.

The body has two standard tabs:

- Capture: a stable file drop region, a multiline text or URL input, and a clear `收进 Inbox` command.
- Today: the local date, category selector, multiline entry input, recent InboxDock Daily entries, and an `追加到 Daily` command.

The footer shows the last successful operation and an undo icon. Empty, busy, success, recoverable-error, and invalid-Vault states have distinct messages without resizing the window.

### Visual Direction

The application is quiet and work-focused. It uses paper white and charcoal surfaces, a restrained green success accent, and amber warnings. It avoids gradients, decorative blobs, oversized typography, deeply nested cards, and one-color styling.

Corners are no more than 8 pixels. Layout dimensions remain stable while content changes. Light and dark palettes follow the Windows application theme. Text is never sized from viewport width.

### Motion

- Expand and collapse: 160 ms opacity and width transition.
- Drag enter: a small border and surface-color change, without pulsing.
- Successful capture: a brief check transition lasting no more than 600 ms.
- Progress: determinate for file copies where total size is known.
- Reduced motion: animations are disabled when Windows requests reduced motion.

## First-Run and Settings

On first launch, InboxDock asks for the Vault root. A valid Vault must be an existing directory containing `.obsidian`.

Defaults for the current Vault are:

```text
Vault: E:\knowledge\data\第一个仓库
Inbox: 00 Inbox收件箱
Daily: 01 Daily日常
Daily template: 10 Knowledge Hub/Templates/Daily.md
Attachments: 05 Resources/Attachments
```

The settings page allows changing these relative paths, always-on-top, launch at sign-in, theme, and saved window position. A path preview shows the resolved target before saving. InboxDock refuses paths that resolve outside the selected Vault.

Application settings and operation history are stored under `%LocalAppData%\InboxDock`, not inside the Vault.

## Capture Data Model

Each operation receives a GUID `captureId` and a local timestamp. History stores:

- operation type
- capture ID
- timestamp
- created Inbox note path, when applicable
- created attachment paths
- Daily note path and exact appended record, when applicable
- undo status

History is a bounded local JSON file. Version 1 retains the latest 100 operations.

## Inbox Capture Workflow

### Text

Text creates a new Inbox note named:

```text
YYYY-MM-DD-HHmmss-<first-meaningful-words>.md
```

Invalid Windows filename characters are removed. Chinese text is preserved. If the target already exists, `-2`, `-3`, and subsequent suffixes are added.

The note format is:

```markdown
---
type: inbox
status: unprocessed
created: 2026-07-13T20:30:00+08:00
source: inboxdock
capture_id: <guid>
---

# <generated title>

## 内容

<captured text>
```

### URL

A URL follows the text workflow and is saved as a normal Markdown link. Version 1 does not fetch a page title or remote metadata.

### Files

Files are copied into:

```text
05 Resources/Attachments/YYYY-MM-DD/
```

The original dropped files remain untouched. Each destination name preserves the original filename and adds a numeric suffix on collision. Files are copied to a temporary name in the destination directory and renamed only after the copy completes.

After all copies succeed, InboxDock creates one Inbox note for the operation. Images use Obsidian embeds; other files use ordinary wiki attachment links. The note includes the original filename, size, and capture time. InboxDock never opens or executes a dropped file.

If one file in a multi-file capture fails, no Inbox note is committed. Completed temporary copies from the same operation are rolled back to the local InboxDock recovery area and the user receives a specific error.

## Daily Workflow

Today's filename uses the local Windows date and `yyyy-MM-dd.md` under the configured Daily directory.

If the note does not exist, InboxDock reads the configured Daily template, replaces `{{date:YYYY-MM-DD}}` and `{{title}}`, and creates the file atomically. If the template is missing, a minimal Daily note with an H1 date heading is created.

InboxDock owns one append-only section at the end of the note:

```markdown
## InboxDock 快速记录

- 19:30 · 学习 · 理解了 Java 接口 <!-- inboxdock:<guid> -->
```

Available categories are `完成`, `学习`, `问题`, and `灵感`. The hidden capture marker permits a precise undo without changing unrelated Daily content.

Before replacing an existing Daily file, InboxDock checks the last-write timestamp and content hash. If Obsidian or another process changes the note during the operation, InboxDock rereads and retries three times with short backoff. It never replaces a newer version with an older in-memory copy.

## Undo

Undo applies only to the latest successful InboxDock operation.

For Inbox captures, created notes and attachment copies are moved to `%LocalAppData%\InboxDock\Recovery\<captureId>` rather than permanently deleted. Original dropped files are never touched.

For Daily captures, InboxDock removes only the exact list item containing the operation's hidden capture marker. If the user edited or removed that item, undo stops and reports that automatic undo is no longer safe.

## Atomicity and Conflict Handling

- New Markdown files are written to a temporary file in the same destination directory, flushed, and renamed atomically.
- Attachment copies use a temporary suffix and are renamed after a complete copy.
- Existing Daily notes use read-hash-modify-verify-replace with three retries.
- All target paths are canonicalized and verified to remain under the selected Vault.
- File and directory collisions never trigger overwrite.
- A failed operation records an error but is not added as an undoable success.
- The UI remains responsive by running file operations asynchronously with cancellation support.

## Obsidian Integration

InboxDock does not require Obsidian to be running. An `在 Obsidian 中打开` action uses an `obsidian://open` URI with the Vault and relative file path. If the protocol is unavailable, InboxDock reveals the file in Windows Explorer.

Codex integration remains deliberately indirect: InboxDock writes normal Markdown and attachments, and the existing Claudian/Codex workflow reads and organizes them later. This keeps capture reliable and avoids adding credentials or agent permissions to the desktop utility.

## Tray and Startup

Closing the expanded window hides it to the tray. The tray menu provides show/hide, capture, Today, settings, and quit commands.

Launch at sign-in is opt-in. It writes a current-user startup entry that points to the published executable. Disabling the option removes only the entry created by InboxDock.

## Error States

Errors are stated in plain language and include a next action:

- Invalid Vault: choose a folder containing `.obsidian`.
- Missing configured folder: offer to create it inside the Vault.
- File locked: retry, then identify the locked path.
- Insufficient space or permission: preserve originals and show the failed destination.
- Template unreadable: create the minimal Daily note and report the fallback.
- Obsidian URI unavailable: reveal the file in Explorer.

InboxDock logs technical details locally without storing captured text or filenames beyond the bounded operation history required for undo.

## Accessibility

- All controls are keyboard reachable.
- The two tabs support standard arrow-key navigation.
- Icon buttons have tooltips and automation names.
- Status changes are announced through an accessible live region.
- Color is not the only error or success signal.
- The interface is checked at 100%, 125%, 150%, and 200% Windows scaling.

## Testing

Unit tests cover path containment, invalid filename characters, Chinese titles, collision suffixes, Markdown generation, template replacement, capture markers, and history limits.

Integration tests create temporary Vaults and cover text, URL, single-file, multi-file, name collision, partial copy failure, Daily creation, Daily append, concurrent change retry, safe undo, invalid Vault, and paths containing spaces and Chinese characters.

UI smoke testing covers drag enter and leave, multi-file progress, both tabs, keyboard navigation, tray show and hide, persisted position, theme, reduced motion, and DPI scaling. Screenshots are inspected at common desktop dimensions to catch clipping and overlap.

The final release is tested with Obsidian running, hidden, and completely closed.

## Packaging and GitHub

The repository uses the MIT License. `README.md` includes installation, first-run configuration, screenshots, privacy behavior, build instructions, and the exact Vault changes InboxDock performs.

Release builds target `win-x64` as self-contained, single-file output. GitHub Actions restores dependencies, builds, runs tests, and uploads the portable artifact for version tags. Version 1 does not require an installer; users extract the release into a stable folder and run `InboxDock.exe`.

## Acceptance Criteria

- The application works with Obsidian fully closed.
- Files, text, and URLs create correct, non-overwriting Inbox records.
- Daily entries are created or appended without losing external changes.
- Undo affects only the latest InboxDock operation and never the original dropped file.
- The compact and expanded windows render without overlap at supported DPI settings.
- Tray, always-on-top, saved position, and optional startup work on Windows 10 and 11.
- All automated tests pass from a clean clone.
- A self-contained `win-x64` release launches on a machine without a separate .NET runtime.
