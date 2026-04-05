# FILA Desktop (C# Native)

Windows WinForms client for managing FILA key workflows using a native .NET 8 implementation.

## Features
- Master key bootstrap from Windows registry with fallback generation.
- AES-encrypted local vault (`vault.enc`) with atomic save and backup fallback.
- Crypto helpers for store-material derivation, handshake decrypt, and mobile envelope generation.
- Indexer for desktop `*LATEST-KEY.FILA` drops.
- WinForms single-window dashboard with device table, scan/update actions, and log console.

## Build
```bash
dotnet restore
dotnet build FilaDesktop.sln
dotnet test FilaDesktop.sln
```

## Notes
- Targets Windows (`net8.0-windows`) for WinForms + WMI + MTP libraries.
