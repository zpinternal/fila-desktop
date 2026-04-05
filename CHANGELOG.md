# Changelog

## 0.2.1 - 2026-04-05 14:53 (GMT+3)

- Refactored `DeviceUtil` to a single ownership model where utility methods take serial IDs and fully own `MediaDevice` creation/disposal.
- Added explicit, exception-safe connect/disconnect behavior with connected-state guards in `DeviceUtil`.
- Added XML ownership-contract documentation for `ListDevices`, `FindFilaFolder`, `PullMobileKey`, and `PushKey`.
- Wired `DeviceTrackerService.RefreshAsync` to poll real devices via `DeviceUtil` and update tracked states.
- Updated `MainForm` device update flow to push the latest vault key to the selected device through `DeviceUtil.PushKey`.

## 0.2.0 - 2026-04-05 11:47 (GMT+3)

- Implemented initial native C# FILA desktop client scaffolding from spec.
- Added WinForms UI with status, device grid, scan/update controls, and logging console.
- Added utilities for master key bootstrap, vault encryption, key derivation, envelope generation, and desktop indexing.
- Added initial xUnit tests for crypto and vault helpers.
- Added repository documentation and agent memory/backlog/design files.
