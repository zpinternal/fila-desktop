# Changelog

## 0.2.6 - 2026-04-05 16:28 (GMT+3)
- Refactored `DeviceUtil` ownership semantics so helper methods no longer dispose caller-provided `MediaDevice` instances.
- Updated `MainForm` device update flow to use a single resolved device handle for pull+push operations, with explicit caller-side disposal.
- Removed re-enumeration-before-push logic that existed only to work around implicit disposal in `DeviceUtil`.

## 0.2.5 - 2026-04-05 16:03 (GMT+3)
- Implemented functional Auto-Update behavior in `MainForm` by wiring the checkbox to a cancellable background worker loop.
- Added an auto-update execution loop that refreshes tracked devices, selects READY/non-cooldown devices, and performs updates continuously while enabled.
- Added update serialization with `SemaphoreSlim` to prevent concurrent manual and auto update operations from overlapping.
- Added startup/shutdown logs for auto-update and ensured cancellation/cleanup on form disposal.

## 0.2.4 - 2026-04-05 15:42 (GMT+3)
- Fixed master-key fallback persistence to use a user registry hive (`HKEY_CURRENT_USER\\SOFTWARE\\FilaDesktop\\MachineGuid`) instead of writing fallback data to machine-wide registries.
- Added compatibility read for the legacy machine fallback path while prioritizing safer per-user storage.
- Hardened registry access with security/authorization exception handling for both read and write operations.

## 0.2.3 - 2026-04-05 15:12 (GMT+3)
- Fixed `DeviceTrackerService.RefreshAsync` overlap race by adding a `SemaphoreSlim` refresh gate that serializes concurrent timer/WMI refresh triggers.
- Converted `RefreshAsync` to a true async method using `WaitAsync`, while preserving batched `DevicesChanged` behavior and cooldown-aware state updates.
- Updated disposal to release the new refresh gate resource.

## 0.2.2 - 2026-04-05 15:09 (GMT+3)
- Fixed `IndexerUtil.Scan` to map imported `DDD-...` payloads to deterministic calendar dates derived from the decrypted day token, instead of always using the current UTC date.
- Added day-token validation (`1..366`) and nearest-year resolution logic (`year-1`, `year`, `year+1`) to handle year-boundary imports safely.
- Added `IndexerUtilTests` coverage for multi-entry import behavior and rejection of non-numeric day prefixes.

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
