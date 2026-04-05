# Backlog

## P0
- Add end-to-end tests with mocked MTP abstraction to cover READY/OUTDATED/FILA NOT FOUND/UPDATED transitions.
- Refactor `DeviceUtil` ownership semantics so `PullMobileKey`/`PushKey` do not force disposal of caller-provided `MediaDevice` instances, allowing a true single-connection update pipeline.
- Add a dedicated lock/semaphore around `RefreshAsync` to prevent overlapping timer + WMI refresh races.

## P1
- Implement auto-update worker that consumes READY devices while respecting cooldown.
- Add integration scenario for corrupted `vault.enc` fallback to `.bak`.

## P2
- Add import deduplication and date extraction from decrypted payload instead of current-date assignment.
- Add explicit UI status color coding by device state.

## P3
- Add packaging scripts and release artifacts for signed Windows builds.
