# Copilot Cleaner

Copilot Cleaner is a local cross-platform C# Avalonia desktop app for reviewing and cleaning GitHub Copilot session-state folders.

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
- Automatically loads and displays SDK-visible Copilot sessions in a separate tab using `GitHub.Copilot.SDK`.
- Sorts Session State and Copilot SDK columns with multi-column sorting applied in visible left-to-right column order and active precedence/direction shown in grid headers.
- Allows column drag-and-drop reordering.
- Aggregates rows by one or more visible columns, with nested group-level checkboxes for bulk selection.
- Moves selected session folders to a configurable target directory.
- Deletes selected session folders after confirmation.
- Deletes selected SDK-visible sessions through the Copilot SDK after confirmation.

## Run

```powershell
dotnet run
```

Press `Ctrl+C` in the terminal to shut down a `dotnet run` session.

## Build

```powershell
dotnet restore CopilotCleaner.csproj
dotnet build CopilotCleaner.csproj --configuration Release
```

To create a framework-dependent publish output for a target runtime:

```powershell
dotnet publish CopilotCleaner.csproj --configuration Release --runtime win-x64 --self-contained false --output artifacts/CopilotCleaner-win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish CopilotCleaner.csproj --configuration Release --runtime linux-x64 --self-contained false --output artifacts/CopilotCleaner-linux-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
dotnet publish CopilotCleaner.csproj --configuration Release --runtime osx-arm64 --self-contained false --output artifacts/CopilotCleaner-osx-arm64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Release

GitHub Actions builds the app for Windows, Linux, and macOS for pushes and pull requests to `main`. Tags that match `v*` run the release workflow, publish self-contained `win-x64`, `win-arm64`, `linux-x64`, `osx-x64`, and `osx-arm64` ZIP artifacts, and create a GitHub Release.

## Cross-Platform Status

The app now targets `net8.0` and uses Avalonia for the desktop UI, including cross-platform folder pickers. The service and model layers use portable .NET filesystem and parsing APIs, while cleanup behavior still depends on the local Copilot session-state layout and Copilot SDK/CLI availability on the host system.

## SDK and File Parsing

The cleaner uses direct file parsing for local `session-state` folder details because those files are the cleanup target for move/delete operations. It uses the official `GitHub.Copilot.SDK` package for Copilot session enumeration and SDK-managed session deletion. The app does not directly read or write the root `session-store.db`.
