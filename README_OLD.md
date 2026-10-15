## New in v0.3.6: First-run Portable / Local choice

Choose **Portable** or **Local** when you first open the app. The choice can be remembered or requested on every launch; change it later in **Advanced → Storage mode and cleanup…**. Portable mode keeps all newly created desktop state alongside the executable. You may review and explicitly remove known legacy PhotoSafe Check AppData files while in Portable mode. Original photos are never deleted. [Storage & cleanup guide](STORAGE_MODES.md).

---

## New in v0.3.5: Browser companion install links

The **Connect Google Photos** step offers two clearly labeled installation paths: Chrome Web Store and GitHub Releases, plus a bundled-folder fallback. Publication URLs are configured in `PhotoSafeCheck/ExtensionLinks.json` and must be real HTTPS destinations. Missing links remain visibly unavailable; no made-up project links are shipped. See [EXTENSION_DISTRIBUTION.md](EXTENSION_DISTRIBUTION.md) for store upload, GitHub releases, and how to activate buttons without changing C# code. This update does **not** change the existing scanner or extension's behavior.

---

# PhotoSafe Check v0.3.5 — connection diagnostics and matching app icons

The attached log shows the font-weight crash was fixed, but `Dispatcher.UIThread` was accessed too early in `Program.Main`, producing `PlatformNotSupportedException` during `Dispatcher.MainLoop`. The dispatcher exception callback now registers **after** Avalonia initialization. This matches [Avalonia's documented troubleshooting for that exact exception](https://github.com/AvaloniaUI/Avalonia/discussions/17672).

**Want to make an unzip-and-run build?** See [PORTABLE_GUIDE.md](PORTABLE_GUIDE.md). The source project itself still requires the .NET SDK to build/run; a **published self-contained bundle** does not. Portable bundles suggest Portable in the first-run chooser using `portable.mode`; the user may select Local instead. Source/debug runs also ask the user on first launch and preserve their previously saved data by default.

**No photos are deleted, renamed, moved, overwritten, or altered.** Staging is an optional user-triggered COPY operation and comparisons are indicative, not guaranteed backup verification.

---

# PhotoSafe Check v0.3.2 — Avalonia FontWeight Startup Fix

The reported `System.ArgumentException: Font weight must be > 0` came from the friendly UI's `Txt` helper. The optional `FontWeight weight = default` supplied a **zero** font weight to Avalonia. v0.3.2 uses a 3-argument overload that explicitly delegates with `FontWeight.Normal`, preserving the 4-argument overload for text intentionally set to Bold/SemiBold.

## Try on Windows

Extract into a **fresh directory**, open PowerShell inside the nested `PhotoSafeCheck` folder and run:

```powershell
dotnet clean
dotnet restore
dotnet build
dotnet run
```

If you prefer a tiny manual patch to your existing v0.3.1 checkout, see `PATCH_INSTRUCTIONS.md`.

**Verification:** Source checks and the existing Node.js extension tests were executed. A .NET SDK is not installed in the creation environment, so runtime startup on Windows and macOS/Linux still requires testing. If the application fails again, the original startup diagnostics remain available in `%LOCALAPPDATA%\PhotoSafeCheck\logs\startup.log`.

The extension and scanner logic are unchanged in this hotfix. The app still never deletes, moves, renames, or modifies original photos; staging only makes separate copies when the user explicitly requests it.

---

# PhotoSafe Check v0.3.1 — Startup Diagnostics Patch

This is the **v0.3 friendly preview with diagnostics added**. It does not intentionally change the scanner, extension or read-only copy behavior.

## Windows PowerShell quick test

Extract into a **new folder**, open PowerShell in the nested `PhotoSafeCheck` folder, then:

```powershell
dotnet clean
dotnet restore
dotnet build
dotnet run
```

**What's different:** `OutputType=Exe` now keeps console errors visible, and an error in the new friendly window produces a basic diagnostic window rather than disappearing. A Windows message box appears for an exception before the window can be created.

Check this diagnostic log if the window still fails to appear:

```powershell
Get-Content "$env:LOCALAPPDATA\PhotoSafeCheck\logs\startup.log" -Tail 65
```

You can see whether the program is still running with `Get-Process PhotoSafeCheck -ErrorAction SilentlyContinue`. If the process is still running without a window, send that detail too.

**Privacy:** The log can include filesystem paths and stack traces when an error occurs. Review it before posting. No images or Google credentials are logged intentionally. Neither this patch nor its diagnostics modify photos.

---

# PhotoSafe Check v0.3 — Friendly Preview

A cross-platform (.NET 10/Avalonia) desktop application with a Chrome/Edge browser companion that compares local filenames with Google Photos search outcomes. **Read-only scanning — no photo deletion, moves, renaming or editing.** An optional separate staging tool creates verified new copies of not-found-by-search files for a user to upload manually.

**Start here:** [Friendly setup guide](SETUP_GUIDE.md) · [Release checklist](RELEASE_CHECKLIST.md) · [Change log](CHANGELOG.md) · [Testing plan](TEST_PLAN.md)

## What's new in v0.3

- Cleaner five-step workflow, persistent getting-started checklist, and four summary counts.
- Scanning progress bar, estimated time remaining, and friendly status messages.
- Searchable, filterable comparison results with an on-demand *local* image preview and **Show in folder**.
- A redesigned Chrome/Edge extension popup with a progress bar; the browser's existing countdown remains intact.
- Troubleshooting/reset actions tucked into an Advanced section; pairing key only shown by clicking Copy.
- Safer full CSV exports that refuse to overwrite an existing file.
- Cross-platform preview build workflow (Windows, Linux, macOS Intel/Apple Silicon).

## Source run

```bash
dotnet restore PhotoSafeCheck/PhotoSafeCheck.csproj
dotnet build PhotoSafeCheck/PhotoSafeCheck.csproj
dotnet run --project PhotoSafeCheck/PhotoSafeCheck.csproj
```

Load the included `Extension/` with your browser's **Load unpacked** option. No Playwright or PowerShell 7 is required. Chrome/Edge must be running in the user's normal logged-in Google Photos session. The extension uses no Google Photos private API or photo-byte upload; website changes may require maintenance.

## Build preview artifacts

Run **Cross-platform preview builds** in GitHub Actions (including the manual workflow dispatch option). Each job creates a self-contained app plus extension and setup guide. This is **not** a signed installer yet; acceptance testing comes before the first stable release.

## Safety and accuracy

- `Possible match` is not a verified backup.
- `Not found by search` is not a guarantee of cloud absence.
- `Needs review` and `Not checked` are preserved as distinct outcomes.
- The app never deletes local or cloud photos, nor modifies originals. The optional staging feature creates **new** copies without overwriting any existing destination.
- Completion remains a three-note optional chime. The Fun edition will be considered **after** the stable release.

## Verification status

The included browser-logic tests pass in Node.js. Desktop compilation and end-to-end tests against real Google Photos accounts and all OSes must be performed outside this environment; do not treat this package as a verified installer or confirmed stable release.


## New in v0.3.5: connection clarity and branding

The desktop and extension now use the same PhotoSafe Check logo. The desktop window displays:

- **Desktop bridge: READY · Waiting for browser** — localhost service is listening; the browser has not connected recently.
- **Extension connected recently** — the browser has successfully authenticated to the local service within 25 seconds.
- **Desktop connection: OFFLINE** — the bridge did not start or could not bind to its port. The error is displayed below the indicator and logged in `startup.log`.

The desktop **Retry connection** control attempts to restart the local service; **Test desktop bridge** checks it directly over localhost using the private pairing key. In the Chrome/Edge extension, a prominent status panel distinguishes **Not paired yet**, **Desktop connected**, **Pairing needs updating**, and **Desktop connection offline**, with a **Check connection again** control.

### If you get a connection error

1. Keep **one** PhotoSafe Check desktop app open. In step 2, read the bridge status. Click **Test desktop bridge**.
2. If it is offline, click **Retry connection** and check its log for the real startup error; another app may be using TCP port `37541`.
3. When the desktop reports READY, open `chrome://extensions` or `edge://extensions`, select **Reload** for PhotoSafe Check, and refresh the Google Photos tab.
4. Open the extension popup, click **Check connection again**. If you see **Pairing needs updating**, use **Copy pairing key** in the CURRENT desktop app and paste it into the extension. Portable and development builds may have different keys.
5. If it still fails, the browser may restrict local service access. Review Chrome/Edge extension permissions and any browser local-network permission prompt; do not bypass browser security or Google sign-in protections.

On Windows, developers can check whether *anything* is listening on the expected port:

```powershell
Get-NetTCPConnection -LocalPort 37541 -State Listen -ErrorAction SilentlyContinue
```

This is diagnostic only and reveals no Google credentials or photo data. Scan reports are still unverified search findings. PhotoSafe Check NEVER deletes, moves, renames or edits original photos.
