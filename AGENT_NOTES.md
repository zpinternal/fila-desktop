# Agent Notes

## 2026-04-05
- Initialized a full C#/.NET project layout for the FILA desktop client from `spec.md`.
- Implemented core utility modules (`VaultUtil`, `CryptoUtil`, `IndexerUtil`, `MasterKeyUtil`) and foundational UI.
- `DeviceTrackerService` includes WMI hook setup + fallback polling scaffolding; detailed WPD enumeration logic is still pending.
- Environment does not include `dotnet`, so format/lint/build/test commands could not run here.
- Added required project governance files and symlinks (`agents.md`, `claude.md`).
- Replaced `MainForm.UpdateSelectedDeviceAsync` placeholder with the full device update workflow: resolve selected serial to a connected MTP device, pull `MOBILE.KEY`, fetch latest vault key, generate envelope, push `\\FILA\\KEYS.FILA`, mark tracker updated, and log success/failure without crashing the UI.
- Because `DeviceUtil.PullMobileKey` and `DeviceUtil.PushKey` dispose the `MediaDevice`, the update flow re-resolves the same serial before push to ensure a live handle.
- Validation commands (`dotnet format`, `dotnet build`) still cannot run in this environment because the .NET SDK is unavailable (`dotnet: command not found`).
- Implemented full `DeviceTrackerService.RefreshAsync` poll cycle using `DeviceUtil.ListDevices()`, stable serial derivation, FILA/MOBILE.KEY state evaluation, and delayed pruning after enumeration.
- Preserved UPDATED cooldown semantics by carrying forward `Updated` state when `InCooldown` is true.
- Adjusted refresh update path to batch `DevicesChanged` notifications (single emit per refresh cycle) while retaining immediate emits for `Upsert`/`MarkUpdated` outside refresh.
