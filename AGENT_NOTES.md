# Agent Notes

## 2026-04-05

- Initialized a full C#/.NET project layout for the FILA desktop client from `spec.md`.
- Implemented core utility modules (`VaultUtil`, `CryptoUtil`, `IndexerUtil`, `MasterKeyUtil`) and foundational UI.
- `DeviceTrackerService` includes WMI hook setup + fallback polling and now polls `DeviceUtil` identities/state; nuanced READY/OUTDATED heuristics are still pending.
- Environment does not include `dotnet`, so format/lint/build/test commands could not run here.
- Added required project governance files and symlinks (`agents.md`, `claude.md`).

- Refactored `DeviceUtil` to Option A ownership: pass serial IDs, instantiate/connect/disconnect/dispose `MediaDevice` internally via a shared helper to avoid mixed ownership bugs.
- `DeviceTrackerService.RefreshAsync` now consumes `DeviceUtil.ListDevices` and resolves FILA folder state per serial; disconnected devices are pruned from cache.
- `MainForm.UpdateSelectedDeviceAsync` now pushes the latest vault key bytes to the selected device and logs failures instead of silently marking updated.
