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
- Shows `Has copilotshell.json`, `workspace.cwd`, `Last modified`, `In Copilot SDK list`, `workspace.git_root`, `workspace.branch`, `workspace.summary`, and `metadata.origin` as first-class main-grid columns.
- Shows all flattened metadata values and a lazily loaded recursive file list for the selected session folder.
- Loads and displays SDK-visible Copilot sessions in a separate tab using `GitHub.Copilot.SDK`.
- Sorts columns with multi-column sorting applied in visible left-to-right column order.
- Allows column drag-and-drop reordering.
- Aggregates rows by one or more visible columns, with nested group-level checkboxes for bulk selection.
- Moves selected session folders to a configurable target directory.
- Deletes selected session folders after confirmation.
- Deletes selected SDK-visible sessions through the Copilot SDK after confirmation.

## Run

```powershell
dotnet run
```

## SDK and File Parsing

The cleaner uses direct file parsing for local `session-state` folder details because those files are the cleanup target for move/delete operations. It uses the official `GitHub.Copilot.SDK` package for Copilot session enumeration and SDK-managed session deletion. The app does not directly read or write the root `session-store.db`.