# Backlog

## P0
- Add end-to-end tests with mocked MTP abstraction to cover READY/OUTDATED/FILA NOT FOUND/UPDATED transitions.
- Refactor `DeviceUtil` ownership semantics so `PullMobileKey`/`PushKey` do not force disposal of caller-provided `MediaDevice` instances, allowing a true single-connection update pipeline.
- Add a dedicated lock/semaphore around `RefreshAsync` to prevent overlapping timer + WMI refresh races.

## P1
- Implement auto-update worker that consumes READY devices while respecting cooldown.
- Add integration scenario for corrupted `vault.enc` fallback to `.bak`.
- Add refresh-trigger coalescing/queue telemetry to observe WMI storm behavior and avoid unnecessary back-to-back scans under high device churn.
- Add auto-update policy controls (max retries per device, backoff strategy, and per-device failure counters) to reduce repeated noisy retries on unstable MTP links.
- Add integration test coverage for single-connection pull+push update flow to validate device-handle lifetime assumptions in `DeviceUtil`/`MainForm`.

## P2
- Add import deduplication and date extraction from decrypted payload instead of current-date assignment.
- Add explicit UI status color coding by device state.
- Add conflict-resolution policy for duplicate `DDD` imports from multiple files (newest file wins vs. first seen wins) and encode it with tests.
- Add migration/telemetry note when master key is sourced from legacy HKLM fallback so deployments can track and eventually remove legacy path dependency.

## P3
- Add packaging scripts and release artifacts for signed Windows builds.
