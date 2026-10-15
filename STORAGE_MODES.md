# Storage choices and cleanup — v0.3.6 preview

On the **first launch**, PhotoSafe Check asks which mode to use. This choice affects only application settings, scan progress, logs and local browser pairing; it never changes photos or Google Photos content. You can leave **Remember my choice** checked, or uncheck it to be asked whenever the app starts. Portable distribution bundles preselect Portable, but both modes remain available.

| | Portable | Local |
|---|---|---|
| Scan history, settings, browser pairing | `Data/` beside the executable beside executable | OS application-data folder (`%LOCALAPPDATA%/PhotoSafeCheck` on Windows) |
| Startup logs | `App/Data/logs/startup.log` | `PhotoSafeCheck/logs/startup.log` in app data |
| Requires writable app folder | Yes | No |
| Suitable for removable/USB folder | Yes | Usually no |
| Leaves app-created state in AppData | No, on a fresh portable installation | Yes, until user explicitly cleans it |

The first-launch chooser does **not create a new AppData folder** just to display the options. Startup diagnostics are held in memory until a mode is chosen. A previously used AppData folder may of course already exist from older versions; the app does not delete it on startup.

**Change modes:** Open **Advanced testing and troubleshooting → Storage mode and cleanup… → Choose storage mode next time I open the app**. Quit and reopen. Select the new mode. You can choose to carry over `state.json` and `pairing-token.txt` to the other location, *only if those destination files do not already exist*. The switch does not overwrite saved data. Already saved state in the selected destination is used preferentially, and older files are left available for recovery. A moved portable folder may require reselecting photo folders if drive letters/paths changed.

**Clear legacy AppData:** While running in Portable mode, open **Storage mode and cleanup… → Review and clean old AppData files…**. A confirmation window lists the only files the app will remove: its own `state.json`, leftover temporary state file, `pairing-token.txt`, `storage-choice.json` and temporary preference file, and two named startup logs. It refuses to follow junctions/symlinks, refuses cleanup while Local mode is active, never recursively deletes folders containing unknown data, and never touches photo folders, staged upload copies, Google Photos, or exported CSV files. It will remove the now-empty PhotoSafeCheck AppData folder where safe. Cleanup is optional and irreversible for the listed app metadata.

**Portable mode is not completely installation-free for the browser companion.** A Chrome/Edge extension still must be installed in the browser profile (Chrome Web Store or manual developer mode). Chrome/Edge may maintain its own extension settings in the browser profile; PhotoSafe Check cannot safely uninstall or purge those on the user's behalf. The desktop app's portable scan state stays inside `App/Data`.

**After selecting Portable mode:** You can remove the unpacked desktop application directory when finished, including `App/Data`. Do not delete the browser extension automatically; use Chrome/Edge's extension manager if you want to remove it. Do not treat any search outcome as proof that a backup is complete.

**Developer preview:** `dotnet run` requires .NET 10 SDK. The self-contained published builds produced by the release scripts do not require the runtime on the target PC. Cross-platform desktop runtime tests are still needed before a stable release.
