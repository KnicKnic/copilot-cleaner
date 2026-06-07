# Architecture

Copilot Cleaner is a local cross-platform desktop app built with C# and Avalonia. It inspects GitHub Copilot session-state folders on disk, lists SDK-visible Copilot sessions, and lets the user decide which session-state folders or SDK sessions to clean up. The project targets `net8.0` so it can build and publish for Windows, Linux, and macOS.

## Project Structure

- `Program.cs`, `App.axaml`, and `App.xaml.cs`: Avalonia desktop application entry point.
- `MainWindow.axaml` and `MainWindow.xaml.cs`: main UI, grid behavior, aggregation controls, selection handling, folder picking, and cleanup commands.
- `Models/`: data models used by the UI.
- `Services/`: filesystem scanning, metadata parsing, row comparison, grouping, and cleanup operations.

## Data Flow

1. The app defaults the source path to `~/.copilot/session-state`, the move target to `~/.copilot/old_session-state`, and the Copilot SDK home to `~/.copilot`.
2. `SessionScanner` enumerates each session folder under the source path.
3. For each session folder, the scanner reads lightweight metadata and top-level filesystem facts into a `SessionRow`.
4. Before a session-state scan, `CopilotSdkSessionService` asks the official GitHub Copilot SDK for the session list so the grid can show whether each folder exists in the SDK-visible session list.
5. `MainWindow` streams rows into the session-state `DataGrid` as they are scanned so the UI becomes useful before the full scan completes.
6. The selected session row lazily loads recursive file entries into the bottom file-list grid.
7. Move and delete commands operate on checked session-state rows, then remove successfully processed rows from the UI.
8. The Copilot SDK Sessions tab loads session metadata with `CopilotClient.ListSessionsAsync` and deletes selected SDK sessions with `CopilotClient.DeleteSessionAsync`.

## Parsing Strategy

The app uses local file parsing for session-state folder details because those cleanup targets are local filesystem artifacts. It uses the official GitHub Copilot SDK for SDK-visible session enumeration and SDK-managed session deletion.

- `vscode.metadata.json` is parsed with `System.Text.Json` and flattened into display columns.
- `copilotshell.json` is parsed with `System.Text.Json`, flattened under the `copilotshell.*` prefix, and surfaced with a file-existence column for quick filtering.
- `workspace.yaml` is parsed with YamlDotNet and flattened into display values. If a session file is malformed, a narrow fallback salvages simple key/value fields and multi-line quoted summaries through end-of-file so fields such as `workspace.summary` can still be displayed.
- `vscode.requests.metadata.json` is summarized as a request count.
- `events.jsonl` is summarized by size during the initial scan. Expensive recursive file enumeration is deferred until a session is selected.
- The Copilot SDK integration does not read or write `session-store.db` directly; it uses `GitHub.Copilot.SDK` APIs.

## UI Composition

The main window has three primary areas:

- A Session State tab with path and action controls for source folder, move target, scan, move, and delete.
- A sortable, reorderable session-state grid with row checkboxes and aggregation controls.
- A selected-session details area with lazy file-list loading and full flattened metadata values.
- A Copilot SDK Sessions tab with SDK home selection, SDK session loading, SDK metadata columns, missing-session-state selection, and SDK-backed deletion.

Sorting is tracked per column and applied according to the visible left-to-right column order. Aggregation is represented by explicit group rows in the session grid, supports multiple ordered grouping levels, and supports group-level checkbox selection across nested groups.

## Safety Boundaries

Delete operations ask for confirmation. Move operations create the destination folder when needed and avoid overwriting existing session folders by adding a numeric suffix. Sessions with `inuse.*.lock` files are surfaced in the grid so active sessions can be avoided. SDK session deletion also asks for confirmation and routes through `CopilotClient.DeleteSessionAsync` rather than direct database mutation.

## Platform Portability

The UI uses Avalonia controls and storage-provider folder pickers rather than WPF or Windows Forms APIs. The service and model layers are designed around .NET filesystem and parsing APIs. Release automation publishes self-contained app artifacts for Windows, Linux, and macOS, but runtime behavior still depends on the host having Copilot session-state data and Copilot SDK/CLI support.

## Automation

GitHub Actions includes a Windows, Linux, and macOS build workflow for pushes and pull requests to `main`. Release automation runs from `v*` tags or manual dispatch, publishes self-contained `win-x64`, `win-arm64`, `linux-x64`, `osx-x64`, and `osx-arm64` ZIP files, and creates a GitHub Release. Dependabot tracks NuGet and GitHub Actions updates weekly.
