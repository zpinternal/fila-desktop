# Backlog

## P0

- Implement full `DeviceTrackerService.RefreshAsync` wiring to `DeviceUtil` and real device state transitions.
- Add end-to-end tests with mocked MTP abstraction to cover READY/OUTDATED/FILA NOT FOUND/UPDATED transitions.

## P1

- Implement auto-update worker that consumes READY devices while respecting cooldown.
- Add integration scenario for corrupted `vault.enc` fallback to `.bak`.

## P2

- Add import deduplication and date extraction from decrypted payload instead of current-date assignment.
- Add explicit UI status color coding by device state.

## P3

- Add packaging scripts and release artifacts for signed Windows builds.

## P1 (added 2026-04-05)

- Add integration tests around `DeviceUtil` connection lifecycle (already connected/disconnected/error paths) using an abstraction over `MediaDevice`.
