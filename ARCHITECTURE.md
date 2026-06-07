# Architecture

Copilot Cleaner is a local Windows desktop app built with C# and WPF. It inspects GitHub Copilot session-state folders on disk and lets the user decide which sessions to move or delete.

## Project Structure

- `App.xaml` and `App.xaml.cs`: WPF application entry point.
- `MainWindow.xaml` and `MainWindow.xaml.cs`: main UI, grid behavior, aggregation controls, selection handling, and cleanup commands.
- `Models/`: data models used by the UI.
- `Services/`: filesystem scanning, metadata parsing, row comparison, grouping, and cleanup operations.

## Data Flow

1. The app defaults the source path to `~/.copilot/session-state` and the move target to `~/.copilot/old_session-state`.
2. `SessionScanner` enumerates each session folder under the source path.
3. For each session folder, the scanner reads lightweight metadata and top-level filesystem facts into a `SessionRow`.
4. `MainWindow` streams rows into the main `DataGrid` as they are scanned so the UI becomes useful before the full scan completes.
5. The selected session row lazily loads recursive file entries into the bottom file-list grid.
6. Move and delete commands operate on checked rows, then remove successfully processed rows from the UI.

## Parsing Strategy

The app uses local file parsing rather than a Copilot service SDK because cleanup targets session-state data already stored on disk.

- `vscode.metadata.json` is parsed with `System.Text.Json` and flattened into display columns.
- `copilotshell.json` is parsed with `System.Text.Json`, flattened under the `copilotshell.*` prefix, and surfaced with a file-existence column for quick filtering.
- `workspace.yaml` is parsed with YamlDotNet and flattened into display values. If a session file is malformed, a narrow fallback salvages simple key/value fields and multi-line quoted summaries through end-of-file so fields such as `workspace.summary` can still be displayed.
- `vscode.requests.metadata.json` is summarized as a request count.
- `events.jsonl` is summarized by size during the initial scan. Expensive recursive file enumeration is deferred until a session is selected.

## UI Composition

The main window has three primary areas:

- Path and action controls for source folder, move target, scan, move, and delete.
- A sortable, reorderable session grid with row checkboxes and aggregation controls.
- A selected-session details area with lazy file-list loading and full flattened metadata values.

Sorting is tracked per column and applied according to the visible left-to-right column order. Aggregation uses WPF collection grouping, supports multiple ordered grouping levels, and supports group-level checkbox selection across nested groups.

## Safety Boundaries

Delete operations ask for confirmation. Move operations create the destination folder when needed and avoid overwriting existing session folders by adding a numeric suffix. Sessions with `inuse.*.lock` files are surfaced in the grid so active sessions can be avoided.