# Copilot Cleaner

Copilot Cleaner is a local C# WPF app for reviewing and cleaning GitHub Copilot session-state folders.

By default it scans:

```text
~/.copilot/session-state
```

and moves selected session folders to:

```text
~/.copilot/old_session-state
```

## Features

- Streams session rows into the grid while scanning continues in the background.
- Reads each session folder and displays high-value cleanup hints from `vscode.metadata.json`, `copilotshell.json`, `workspace.yaml`, and top-level session files.
- Shows `Has copilotshell.json`, `workspace.cwd`, `Last modified`, `workspace.git_root`, `workspace.branch`, `workspace.summary`, and `metadata.origin` as first-class main-grid columns.
- Shows all flattened metadata values and a lazily loaded recursive file list for the selected session folder.
- Sorts columns with multi-column sorting applied in visible left-to-right column order.
- Allows column drag-and-drop reordering.
- Aggregates rows by one or more visible columns, with nested group-level checkboxes for bulk selection.
- Moves selected session folders to a configurable target directory.
- Deletes selected session folders after confirmation.

## Run

```powershell
dotnet run
```

## SDK vs file parsing

The cleaner uses direct file parsing because the target data is local session-state on disk. The GitHub Copilot SDK is useful for building Copilot-powered integrations, but it is not needed for moving or deleting local session-state folders. Keeping this as file parsing also makes cleanup work without requiring a network call or Copilot service authentication.