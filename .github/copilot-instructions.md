# Repository Instructions

This project is a local C# WPF app for inspecting and cleaning GitHub Copilot session-state folders.

## Documentation Updates

For every meaningful code, UI, data-model, file-system behavior, or project-structure change, update the project documentation in the same change.

- Update `ARCHITECTURE.md` when the app structure, major responsibilities, data flow, storage assumptions, parsing strategy, UI composition, or operational boundaries change.
- Update `FEATURES.md` when user-visible behavior, supported cleanup actions, displayed metadata, sorting/grouping behavior, safety prompts, defaults, or limitations change.
- Update `README.md` when setup, run commands, defaults, usage guidance, or project overview changes.
- If `ARCHITECTURE.md` or `FEATURES.md` does not exist yet and the change introduces information that belongs there, create the missing file.
- Keep documentation concise and factual. Prefer updating existing sections over duplicating the same information in multiple places.
- Mention when a change intentionally does not require documentation updates only if that might be surprising.

## Implementation Notes

- Keep the app dependency-light unless a dependency clearly improves reliability or maintainability.
- Prefer local filesystem parsing for Copilot session-state cleanup unless a task explicitly requires a service API.
- Treat move and delete operations cautiously, especially when `inuse.*.lock` files are present.