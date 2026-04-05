# Software Specification: FILA Store Client (C# Native)

## 1. Introduction & Overview
The **FILA Store Client** is a native Windows application built in C# (.NET 8). It acts as a secure intermediary to manage, decrypt, and deploy security keys to connected Android MTP devices. 

This application replaces a legacy stack (Python/Flask/PowerShell/SQLite) with a zero-dependency, compiled native binary. 

### 1.1 Core Objectives
*   **Zero External Dependencies:** Eliminates reliance on Python environments or PowerShell scripts.
*   **Native MTP Communication:** Communicates directly with Android devices via the Windows Portable Devices (WPD) API.
*   **Robust Flat-File Vault:** Replaces SQLite with a resilient, atomic AES-encrypted JSON file.
*   **Asynchronous & Responsive:** Utilizes `Task.Run` and `async/await` patterns to ensure a fluid UI, replacing legacy blocking wait loops.
*   **Intelligent Device Tracking:** Maintains a real-time state machine for connected hardware via WMI/Win32 event hooks.

---

## 2. Application Initialization & Identity

Before the core logic initializes, the application must identify the host machine uniquely. This unique identifier acts as the **Master Key**, utilized in cryptographic operations to identify payloads meant specifically for this machine.

### 2.1 Master Key Bootstrapping Strategy
The system attempts to retrieve or safely generate the Master Key using the following prioritized steps:

1.  **Attempt Primary Registry Retrieval:**
    *   Target: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography` -> `MachineGuid`.
    *   If a valid string is found, assign it as the **Master Key**.
2.  **Fallback to Obscured Registry (ShellIcons):**
    *   Target: `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\ShellIcons`.
    *   If `MachineGuid` is inaccessible or missing, check this path. If a valid GUID string exists here, assign it as the **Master Key**.
3.  **Generation & Obscured Storage:**
    *   If both above fail, generate a new random UUID (`Guid.NewGuid().ToString()`).
    *   Save this newly generated UUID to the `ShellIcons` registry path to hide it from standard user copying/tampering.
    *   Assign this generated UUID as the **Master Key**.

---

## 3. Architecture: "The Helper Model"
The application logic is strictly separated into **Pure Utility Classes**. These classes must remain side-effect free where possible, returning values or results rather than holding internal static state. State is managed at the application/UI layer and passed down as arguments.

| Module | Responsibility | Testability |
| :--- | :--- | :--- |
| `VaultUtil` | Manages atomic read/write and encryption of the local `.enc` vault. | High (Mock strings) |
| `CryptoUtil` | Handles RSA/AES Handshake and Mobile Envelope generation. | High (Known keys) |
| `DeviceUtil` | Abstraction for WPD/MTP (Connect, find folders, push/pull files). | Medium (Requires Hardware) |
| `IndexerUtil` | Scans directories for `LATEST-KEY.FILA` payload drops. | High (Mock Folders) |

---

## 4. Data Specifications

### 4.1 Local Vault (`vault.enc`)
Stores historical and current daily keys locally.
*   **Format:** Flat Binary File.
*   **Symmetric Encryption:** AES-128-CBC.
*   **Vault KDF:** `SHA256("TextPOLOTo07252613")` $\rightarrow$ Truncated to the first 16 bytes.
*   **Binary Structure:** `[16-byte IV] + [AES-Ciphertext (Encrypted JSON Payload)]`.
*   **Key Format:** Keys are formatted as `DDD-...` where `DDD` is the 1-indexed day of the year (e.g., `001` for Jan 1, `366` for Dec 31 leap year).
*   **JSON Schema (Pre-encryption):**
    ```json
    {
      "2023-10-27": "300-dailykeydata123...",
      "2023-10-28": "301-nextkeydata456..."
    }
    ```

### 4.2 Mobile Handshake (`KEYS.FILA`)
The binary envelope pushed to the Android device. Generated on the fly.
*   **Format:** `[RSA-Encrypted Session Key (256b)] + [AES IV (16b)] + [AES-Ciphertext]`.
*   **Encryption Scheme:** RSA (PKCS1 v1.5) + AES-256-CBC (PKCS7 Padding).

---

## 5. Functional Utilities Specification

### 5.1 `VaultUtil.cs`
Responsible for local vault IO.
*   **`Save(string date, string key)`:** Must implement strict **Atomic Writing** to prevent corruption during power loss:
    1. Encrypt updated JSON payload and write to `vault.enc.tmp`.
    2. Move existing `vault.enc` to `vault.enc.bak`.
    3. Rename `vault.enc.tmp` to `vault.enc`.
*   **`Load()`:** Reads, decrypts, and parses the JSON vault. If `vault.enc` is corrupted or missing, it automatically falls back to `vault.enc.bak`.
*   **`GetLatest()`:** Sorts the dictionary keys lexicographically and returns the most recent key value.

### 5.2 `CryptoUtil.cs`
Responsible for payload decryption and mobile envelope generation.

**A. Material Derivation (`DeriveStoreMaterial`)**
Replicates the proprietary KDF to unlock payloads.
1.  **Public ID:** `SHA1(MasterKey)` $\rightarrow$ Returns 20 bytes. Used to scan `LATEST-KEY.FILA` for lines targeting this machine.
2.  **Work Factor (`NN`):** Extract the first two bytes (Index 0, 1) of the SHA1 hash. Parse as a Big-Endian 16-bit integer.
3.  **Iteration Loop:** Hash the `MasterKey` using native `.NET 8` `SHA3_512` exactly `NN` times iteratively.
4.  **Final Key:** Truncate the resulting 512-bit hash to the first 16 bytes $\rightarrow$ **AES-128 Decryption Key**.

**B. Handshake Decryption (`DecryptHandshakeLine`)**
Parses base64 lines from `LATEST-KEY.FILA`.
1.  **Decode:** Base64 to byte array.
2.  **Verify Target:** Check if bytes `[0-19]` strictly match the local Store SHA1 Public ID. If false, discard.
3.  **Extract IV:** Bytes `[20-35]` are the AES IV.
4.  **Decrypt Key:** Bytes `[36+]` are decrypted via **AES-128-CBC**.
5.  **Sanitize:** Apply `.Trim()` to the decrypted string to strip legacy space-padding.

**C. Mobile Envelope Generation (`GenerateMobileEnvelope`)**
Creates the hybrid-encrypted `KEYS.FILA` payload.
1.  **Import Target Key:** Use `rsa.ImportFromPem()` to load the Android device's `MOBILE.KEY` string.
2.  **Generate Session:** Create a random 32-byte AES Session Key and a 16-byte IV.
3.  **Encrypt Session:** RSA-encrypt the Session Key using the device's Public Key (PKCS1 padding).
4.  **Encrypt Secret:** AES-256-CBC encrypt the **Daily Key** using the Session Key + IV (PKCS7 padding).
5.  **Concatenate:** Return `[RSA_Cipher] + [AES_IV] + [AES_Cipher]`.

### 5.3 `IndexerUtil.cs`
*   **`Scan()`:** Scans `%USERPROFILE%\Desktop\FILA` for files matching `*LATEST-KEY.FILA`. Parses contents, feeding lines into `CryptoUtil.DecryptHandshakeLine` to find new keys to push into `VaultUtil`.

### 5.4 `DeviceUtil.cs`
Handles the Windows Portable Device COM interactions.
*   **Strict Memory Management:** Every connection to a `MediaDevice` **must strictly mandate** the use of `using` blocks. COM references must be disposed immediately after the transaction to prevent locking the Android MTP service.
*   **`ListDevices()`:** Yields connected MediaDevices.
*   **`FindFilaFolder(MediaDevice d)`:** Scans root storage endpoints for a directory named `FILA`.
*   **`PushKey(MediaDevice d, byte[] data)`:** Synchronously streams byte array to `FILA/KEYS.FILA`.
*   **`PullMobileKey(MediaDevice d)`:** Reads `FILA/MOBILE.KEY` into a string.

---

## 6. Device State Management (The Tracker)

The application maintains a concurrent state index (Cache Dictionary) mapped by the Device's Serial ID.

### 6.1 Polling & Event Hooks
*   **Primary Trigger:** Utilize WMI/Win32 Event hooks (e.g., `ManagementEventWatcher`) targeting USB Plug/Unplug events.
*   **Fallback:** If WMI fails, gracefully fall back to a 1-second interval WPD polling loop.

### 6.2 State Machine
Every tracked device resides in one of the following states:
*   **`READY`**: `FILA` directory and `FILA/MOBILE.KEY` both exist on the device.
*   **`OUTDATED`**: `FILA` directory exists, but `MOBILE.KEY` is missing.
*   **`FILA NOT FOUND`**: `FILA` directory does not exist at the storage root.
*   **`UPDATED`**: Envelope was successfully generated and pushed to `KEYS.FILA`.

### 6.3 Lifecycle & Cooldown Rules
*   **Disconnection:** If a tracked Serial ID disappears from the system hardware list, purge it from the cache.
*   **Cooldown:** Upon transitioning to the `UPDATED` state, the device timestamp is logged. The Auto-Update engine will **ignore** this Serial ID for **1 minute** to prevent spam-pushing over MTP.
*   **Cooldown Reset:** If a device is disconnected (purged) and reconnected, the cooldown is reset, and it evaluates from scratch.

---

## 7. User Interface (WinForms)

The UI must be exclusively driven by a single-window standard layout. Heavy lifting must occur on background threads.

### 7.1 Layout Requirements
1.  **Top Navigation / Status Bar:**
    *   Label: `Found [X] Keys in Vault`
    *   Label: `Host Key ID: [Derived SHA1 Hash of Master Key]`
2.  **Main Content Area:**
    *   `ListView` or `DataGridView`: Displays connected devices.
        *   Columns: `Device Name`, `Serial ID`, `State`.
    *   `Button`: **[Scan Devices]** (Manual WPD refresh).
3.  **Action Bar (Bottom):**
    *   `Button`: **[Update Selected Device]** (Enabled only if selected state == `READY`).
    *   `CheckBox`: **[Auto-Update]** (Toggles the background thread consumer).
4.  **Logging Console:**
    *   `RichTextBox`: Read-only, auto-scrolling log of events (Info, Success, Error) with timestamps.

### 7.2 Threading Guidelines
*   **No UI Freezing:** Background polling, `MediaDevice` IO, and Cryptographic derivation must run inside `Task.Run` or `async` methods.
*   **Cross-Thread Operations:** All UI updates generated by the background tracker must be marshaled to the main thread via `Invoke` or `BeginInvoke`.

---

## 8. Detailed Handshake Workflow

This outlines the chronological flow of a successful automated deployment:
1.  **Hardware Event:** User connects "SAMSUNG-SM-G991U". WMI Event fires.
2.  **Detection:** Tracker wakes up, identifies the new Serial ID.
3.  **Evaluation:** App creates a `using` block for the `MediaDevice`, discovers `FILA` folder and `MOBILE.KEY`. State is updated to `READY`.
4.  **Trigger:** The Auto-Update loop sees a device in `READY` state.
5.  **Key Retrieval:** `DeviceUtil.PullMobileKey()` securely extracts the Android RSA key.
6.  **Vault Check:** `VaultUtil.GetLatest()` yields the current `DDD-` payload.
7.  **Crypto Phase:** `CryptoUtil.GenerateMobileEnvelope()` packages the payload.
8.  **MTP Push:** `DeviceUtil.PushKey()` writes the binary to `FILA/KEYS.FILA`.
9.  **Post-Processing:** Device state changes to `UPDATED`. A 60-second cooldown is enforced. Device `using` block disposes.
10. **UI Update:** Appends to log: `[2023-11-01 14:00:00] Updated SAMSUNG-SM-G991U successfully.`

---

## 9. Technical & Programming Guidelines

*   **Target Framework:** .NET 8 (Windows-only target).
*   **NuGet Dependencies:**
    *   `MediaDevices` (For MTP/WPD Wrapper).
    *   `Newtonsoft.Json` (For Vault payload serialization).
    *   `System.Management` (For WMI USB event hooks).
*   **Path Formatting:** Use `System.IO.Path` combined with WPD standard slash notation to ensure cross-compatibility between Windows paths and Android MTP paths.
*   **Separation of Keys (Security Warning):**
    *   *Do not confuse keys.*
    *   `StaticLocalKey` ("TextPOLO...") is strictly for local `vault.enc` encryption.
    *   `MasterKey` (UUID) is strictly for identifying and decrypting `LATEST-KEY.FILA`.

---

## 10. Quality Assurance & Independent Test Cases

Developers must implement or manually verify the following isolated scenarios:

1.  **The Vault Atomic Test:** Run a save operation. Force-kill the process via Task Manager exactly when `vault.enc.tmp` is created. Relaunch app. Verify it recovers state from `vault.enc.bak` seamlessly.
2.  **Device State Memory Test:** Connect an Android MTP device. Wait for `READY`. Disconnect it physically. Verify it drops from the UI cache. Reconnect. Verify it re-evaluates properly.
3.  **Crypto Output Size Test:** Supply `GenerateMobileEnvelope` with a known 2048-bit RSA PEM and a 50-byte text payload. Verify the output byte array strictly equals `256 (RSA)` + `16 (IV)` + `64 (Padded AES)` = `336 bytes`.
4.  **Derivation Handshake Test:** Provide the Indexer a mocked `LATEST-KEY.FILA` file containing known payloads mapped to a static `MasterKey`. Ensure the application successfully derives the AES key and extracts the expected `DDD-` key string.
5.  **MTP Lockout Test:** Programmatically push 50 files sequentially to a connected device in a tight loop. Ensure no COM / MTP timeout exceptions occur, proving that `using` block disposal is functioning properly.
