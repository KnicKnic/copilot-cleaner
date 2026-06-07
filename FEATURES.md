# Features

## Session Discovery

- Defaults to scanning `~/.copilot/session-state`.
- Defaults move operations to `~/.copilot/old_session-state`.
- Allows browsing for both source and destination folders.
- Displays an empty-state status message when no session folders are found.
- Streams session rows into the grid while scanning continues in the background.

## Displayed Session Data

The main grid displays high-value filesystem fields and cleanup-oriented summaries. Full flattened metadata remains available in the selected-session details area.

- `Has copilotshell.json`, `workspace.cwd`, last modified time, `workspace.git_root`, `workspace.branch`, `workspace.summary`, and `metadata.origin` as first-class visible columns.
- Folder name, full path, last modified time, and total size.
- Presence of `copilotshell.json`, `vscode.metadata.json`, and `workspace.yaml`.
- File count, directory count, top-level files, and top-level folders from the session root.
- `events.jsonl` size.
- `vscode.requests.metadata.json` request count.
- Presence of `session.db`, `plan.md`, and `inuse.*.lock` files.
- Recursive item counts for `checkpoints`, `files`, `research`, and `rewind-snapshots`.

## Grid Interaction

- Each session row has a checkbox for cleanup selection.
- Pressing Space in the session grid toggles the focused row or all selected rows.
- A select-all-visible checkbox toggles all currently loaded session rows.
- Columns are sortable.
- Columns can be reordered by drag and drop.
- Columns can be resized from fixed starting widths with smaller minimum widths.
- Multi-column sorting is applied using visible left-to-right column order.
- Rows can be aggregated by multiple displayed columns in an ordered hierarchy.
- Aggregation levels can be reordered or removed before clearing the full grouping.
- Aggregated groups have group-level checkboxes for bulk selection.

## File List

- Selecting a session lazily loads its recursive file and folder list.
- The file list includes kind, name, relative path, size, and last modified time.
- File-list columns are sortable, including numeric sorting by raw byte size.

## Metadata Details

- Selecting a session displays all flattened `vscode.metadata.json`, `copilotshell.json`, and `workspace.yaml` values in a details tab.
- Metadata detail columns are sortable by key or value.

## Cleanup Actions

- Move selected session folders to the configured destination folder.
- Avoid overwriting moved folders by adding a numeric suffix when needed.
- Delete selected session folders after confirmation.
- Report per-session filesystem failures without removing failed rows from the grid.

## Current Limitations

- YAML parsing uses YamlDotNet for normal `workspace.yaml` files.
- Malformed multi-line quoted workspace summaries are salvaged through end-of-file when no closing quote is present.
- The app does not currently block move or delete actions for sessions that have `inuse.*.lock`; it surfaces the lock status for user judgment.
- The app does not inspect `session.db` contents.