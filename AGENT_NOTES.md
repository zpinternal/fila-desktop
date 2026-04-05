# Agent Notes

## 2026-04-05
- Initialized a full C#/.NET project layout for the FILA desktop client from `spec.md`.
- Implemented core utility modules (`VaultUtil`, `CryptoUtil`, `IndexerUtil`, `MasterKeyUtil`) and foundational UI.
- `DeviceTrackerService` includes WMI hook setup + fallback polling scaffolding; detailed WPD enumeration logic is still pending.
- Environment does not include `dotnet`, so format/lint/build/test commands could not run here.
- Added required project governance files and symlinks (`agents.md`, `claude.md`).
