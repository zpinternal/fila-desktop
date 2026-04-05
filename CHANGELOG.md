# Changelog

## 0.2.1 - 2026-04-05 14:52 (GMT+3)
- Replaced `UpdateSelectedDeviceAsync` placeholder with a real end-to-end update sequence using `DeviceUtil.PullMobileKey`, `VaultUtil.GetLatest`, `CryptoUtil.GenerateMobileEnvelope`, and `DeviceUtil.PushKey`.
- Added safe background execution with UI-thread marshaling (`Invoke`/`BeginInvoke`) for selection reads, tracker state updates, and logs.
- Added resilient failure logging when the device disconnects, vault key is missing, or an exception occurs, preventing UI crashes.
- Replaced `DeviceTrackerService.RefreshAsync` placeholder logic with a full device poll cycle backed by `DeviceUtil.ListDevices()`.
- Added per-device serial mapping, FILA folder / `MOBILE.KEY` checks, and state assignment for READY, OUTDATED, and FILA NOT FOUND.
- Preserved UPDATED cooldown behavior during refresh and batched refresh notifications to emit `DevicesChanged` once per cycle.

## 0.2.0 - 2026-04-05 11:47 (GMT+3)
- Implemented initial native C# FILA desktop client scaffolding from spec.
- Added WinForms UI with status, device grid, scan/update controls, and logging console.
- Added utilities for master key bootstrap, vault encryption, key derivation, envelope generation, and desktop indexing.
- Added initial xUnit tests for crypto and vault helpers.
- Added repository documentation and agent memory/backlog/design files.
