# PhotoSafe Check — clean standalone portable downloads

The normal **Windows download** contains only:

```text
PhotoSafeCheck/
  PhotoSafeCheck.exe
```

Linux and macOS archives have the same folder with a platform-native `PhotoSafeCheck` executable instead. They do not need a separate .NET runtime. Icons, chime, and default GitHub Releases link are embedded. The browser extension is a separate download.

## First run

The application asks where it should save settings, search results, logs and the browser pairing key:

- **Portable (suggested):** creates `PhotoSafeCheck/Data/` beside the executable. Move the entire `PhotoSafeCheck/` folder to another writable location or USB drive to retain progress. Delete the folder to remove portable application state. The folder must be writable.
- **Local:** saves to the current user's application-data directory, useful when the application lives in a read-only system location. The app can show and clean up known application-created files after explicit confirmation.

The downloaded archive contains neither `Data` nor other user information. Users' original photos are never modified or deleted. Copying not-found photos for manual upload is a separate, opt-in action that creates new copies.

## Installing the extension

The browser companion is **not in the desktop archive**. Install it from the Chrome Web Store when published, or obtain `PhotoSafeCheck-extension.zip` from the GitHub Release, extract it, and use **Load unpacked** in `chrome://extensions` / `edge://extensions` with Developer mode on. The standalone executable has an embedded GitHub Releases URL when built by GitHub Actions. An optional `ExtensionLinks.json` placed beside the executable can override links, but is not required.

## Building from source

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable.ps1 -Rid win-x64
```

The published output in `out/PhotoSafeCheck-win-x64-portable/PhotoSafeCheck/` includes only the executable. GitHub Actions creates the clean, platform-specific ZIP or tarball automatically. Source development still requires .NET and includes all source files.

## Updating a portable copy

Close the app. Back up its `Data/` directory, and replace **only** `PhotoSafeCheck.exe` with the newer executable from a trusted download. Do not replace/delete `Data/` unless you intentionally want to reset your results, pairing and settings. If coming from older packages that used `App/Data/`, close both apps and use the storage chooser's carry-over option, or manually copy app-owned state after reviewing destination files.

## Notes

A .NET self-contained single-file executable may extract native runtime components to an OS-managed temporary cache; it is not a guarantee of *zero* OS-level temporary files. This does not mean PhotoSafe Check stores scan history in AppData when Portable mode is selected. The Chrome extension also maintains its own settings in the browser profile until removed. Portable storage mode controls PhotoSafe Check's own persistent state.

macOS Gatekeeper and Windows SmartScreen may warn about unsigned builds. Cross-platform compatibility and code signing must be tested before public stable release.
