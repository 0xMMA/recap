# AGENTS.md — Recap

## Workflow

### Releasing

Every release gets a **new version number**. Never retag or overwrite an existing version.

When the user says "ship it", "release it", "push a release", or similar:

1. Determine the version bump using semver:
   - **patch** (0.1.0 → 0.1.1): bug fixes, error message improvements, minor tweaks
   - **minor** (0.1.0 → 0.2.0): new features, new keybinds, new UI elements
   - **major** (0.1.0 → 1.0.0): breaking changes, major rewrites, v1 launch
2. Update `<Version>` in `src/Recap.Desktop/Recap.Desktop.csproj`
3. Commit the version bump with the other changes
4. Tag as `vX.Y.Z` and push tag — CI handles the rest
5. Do **not** delete or move old tags/releases
6. Write polished release notes — features, fixes, keybind tables, install instructions. No bare auto-generated changelogs.

### Commits

- One commit per logical change when possible
- Commit message: imperative, concise, explains *why* not just *what*
- Bug fixes and features in separate commits

### Building

- .NET 10, Avalonia 11, win-x64 self-contained
- `dotnet build` must pass with zero errors before commit
- `dotnet test` must pass before push
- Velopack handles portable zip + self-update packaging via CI

### Project structure

- `src/Recap.Core/` — all non-UI logic (audio, API, config, models, state, logging, updates)
- `src/Recap.Desktop/` — Avalonia 11 UI (MVVM, views, controls, converters)
- `tests/Recap.Tests/` — xUnit + Shouldly tests

### Code

- MVVM with CommunityToolkit.Mvvm (ObservableObject, RelayCommand)
- All file I/O: use `FileShare.ReadWrite` on reads to avoid locking crashes
- File delete/replace: retry 3× with 100ms delay
- Never crash on recoverable errors — show in status bar instead
- Custom controls use Avalonia `DrawingContext` for rendering
