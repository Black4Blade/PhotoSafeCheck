## v0.3.6 change: Portable is now a first-run choice

The `portable.mode` marker in a packaged build **suggests** Portable but does not force it; the first-run chooser lets you select Portable or Local. You can change modes on next startup from the app. See [STORAGE_MODES.md](STORAGE_MODES.md).

# PhotoSafe Check — Portable mode (v0.3.5 preview)

## What portable means

A portable **published** download contains an `App/` folder with the platform-specific executable, a `portable.mode` marker, and the required libraries. The `Extension/` folder contains the browser companion.

- **Windows**: double-click `App/PhotoSafeCheck.exe` (no .NET installation required).
- **Linux**: run `App/PhotoSafeCheck` on a compatible distribution (some OS system libraries may still be needed).
- **macOS**: run `App/PhotoSafeCheck` for the matching architecture (macOS may require a one-time security approval for unsigned software).

All settings, comparison results, the local browser-pairing token, and startup logs are written under `App/Data/` when `App/portable.mode` exists. This folder does not exist in the downloaded ZIP: the app creates it on first launch. Copy the entire portable directory to another writable location/USB drive to retain progress. **No images are stored here by the scanner**; result records contain original file paths, so those paths must still exist to resume or stage photos.

For security, keep the `Data` folder private. It contains a pairing token that allows the browser extension to communicate with the local scanner. Do not publish a ZIP of your used portable app folder.

The browser extension is separately loaded unpacked through `chrome://extensions` or `edge://extensions`. The app never signs into Google; sign into Google Photos normally in Chrome/Edge. The browser may ask you to reload an extension whose directory has moved.

## Build a standalone portable bundle

The **source ZIP** is NOT standalone. To create a self-contained portable bundle, on a development machine with **.NET 10 SDK** and Python 3 installed:

```powershell
# From repository root, on Windows PowerShell
.\scripts\build-portable.ps1 -Rid win-x64
```

The bundle is produced under `out/PhotoSafeCheck-win-x64-portable/`. Zip that **new bundle**, not the `PhotoSafeCheck/` source directory. Separate builds are required for `linux-x64`, `osx-x64`, and `osx-arm64`; the GitHub Actions workflow can generate these on corresponding runners. Published builds have .NET bundled and do not require an SDK or runtime on your friend's computer.

## Migrating existing v0.3.2 scan history

Old development builds stored data under `%LOCALAPPDATA%\PhotoSafeCheck` on Windows. To keep that history, **close both versions**, launch the new portable app once to create `App/Data`, close it, and manually COPY (don't move) `state.json` and `pairing-token.txt` into `App/Data`. Confirm the target files do not contain work you need before replacing them. Reopen PhotoSafe Check and update the extension pairing if necessary. Do not copy a used `Data` folder into public release bundles.

## Updates and safety

When updating a portable copy, preserve your existing `App/Data` folder. Never extract a pre-populated Data directory over it (release bundles intentionally exclude Data). Back up your `Data` folder before upgrades. Original photos are never deleted/moved/renamed/modified. `Copy not-found photos` only creates new copies in a separate staging folder after user action.

## Diagnostic logs

- Portable: `App/Data/logs/startup.log`
- Nonportable source/development run: `%LOCALAPPDATA%\PhotoSafeCheck\logs\startup.log` (Windows)

Source checks are not proof of an actual Windows/macOS/Linux GUI launch. Test the release builds on the target machines before distributing a stable release.

## Extension download buttons

The portable `App/ExtensionLinks.json` file is included beside the executable. Set valid
Chrome Web Store and/or GitHub Releases HTTPS URLs after publishing and restart the app.
Missing links remain disabled; use the included `Extension/` folder in the meantime.
See `EXTENSION_DISTRIBUTION.md` for examples.
