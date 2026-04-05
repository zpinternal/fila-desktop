# Design Notes

## Architecture
The app follows a helper-model split:
- `VaultUtil`: encrypted JSON vault storage with atomic persistence.
- `CryptoUtil`: deterministic material derivation and envelope generation.
- `IndexerUtil`: scans Desktop/FILA drops and extracts keys.
- `DeviceUtil`: MTP/WPD operations (list, inspect, pull/push).
- `DeviceTrackerService`: state cache + event/poll based refresh.

## UI
Single WinForms window with:
- status bar (`Found X keys`, host key id)
- device grid (`Device Name`, `Serial ID`, `State`)
- manual scan, update selected, auto-update toggle
- logging pane

## Security
- Local vault key is static per spec and separate from machine master key.
- Store identification key derives from SHA1(master key).
- Mobile envelope is hybrid RSA+AES.
