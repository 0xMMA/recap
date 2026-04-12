# AGENTS.md — Recap

## Workflow

### Releasing

Every release gets a **new version number**. Never retag or overwrite an existing version.

When the user says "ship it", "release it", "push a release", or similar:

1. Determine the version bump using semver:
   - **patch** (0.1.0 → 0.1.1): bug fixes, error message improvements, minor tweaks
   - **minor** (0.1.0 → 0.2.0): new features, new keybinds, new UI elements
   - **major** (0.1.0 → 1.0.0): breaking changes, major rewrites, v1 launch
2. Update `<Version>` in `src/Recap/Recap.csproj`
3. Commit the version bump with the other changes
4. Tag as `vX.Y.Z` and push tag — CI handles the rest
5. Do **not** delete or move old tags/releases
6. Write polished release notes — features, fixes, keybind tables, install instructions. No bare auto-generated changelogs.

### Commits

- One commit per logical change when possible
- Commit message: imperative, concise, explains *why* not just *what*
- Bug fixes and features in separate commits

### Building

- .NET 10, win-x64 self-contained
- `dotnet build` must pass with zero errors before commit
- `dotnet test` must pass before push
- Velopack handles portable zip + self-update packaging via CI

### Code

- Target Windows only (NAudio/WASAPI, DPAPI, Win32 interop)
- All file I/O: use `FileShare.ReadWrite` on reads to avoid locking crashes
- Never crash on recoverable errors — show in status bar instead
