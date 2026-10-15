## New first-run storage step (v0.3.6)

On first launch, choose **Portable** (app folder, best for ZIP/USB) or **Local** (OS application data, best for read-only install locations). You can remember the choice or be asked every run. See [STORAGE_MODES.md](STORAGE_MODES.md).

## Choosing how to install the companion

From **Connect Google Photos**, click **Get from Chrome Web Store** after publication,
or **Download from GitHub Releases** for the manual ZIP. Extract the GitHub ZIP,
visit `chrome://extensions` or `edge://extensions`, enable Developer mode and choose
**Load unpacked**. If the links are not published yet, use **Open included extension folder**.

# PhotoSafe Check — Friendly setup guide

PhotoSafe Check helps you find **possible gaps** between a local folder and Google Photos. It **never deletes, moves, changes or replaces original photos**. Search matches are hints, not backup guarantees.

## Install from a preview build (after GitHub Actions builds succeed)

1. Download the artifact for your computer from the **Cross-platform preview builds** workflow:
   - `win-x64`: Windows PC (x64)
   - `linux-x64`: most 64-bit Linux PCs
   - `osx-arm64`: Apple Silicon Macs
   - `osx-x64`: Intel Macs
2. Unzip the artifact. Inside are `App/`, `Extension/` and this guide.
3. Start `App/PhotoSafeCheck.exe` on Windows or `App/PhotoSafeCheck` on Linux/macOS. Linux may need `chmod +x App/PhotoSafeCheck`. On macOS, there is **not yet a notarized `.app` bundle**; unsigned preview restrictions may apply.
4. Select a folder containing some test photos and click **Index local files**.
5. Open Chrome or Edge, navigate to `chrome://extensions` or `edge://extensions`, enable **Developer mode**, click **Load unpacked**, and choose the extracted `Extension` folder.
6. Go to https://photos.google.com and sign in as usual. The extension never handles your Google password.
7. In PhotoSafe Check, click **Copy pairing key**, paste it in the extension popup, click **Connect to desktop**, then **Use this Photos tab**.
8. In the desktop app, click **Start / resume checking**. Keep Google Photos open. Results are saved locally, so you can pause and resume.
9. Review all results using search/filter and the local image preview.
10. For manual upload preparation, use **Copy not-found files to upload folder**. Confirm that you have enough free disk space. These new copies are only *upload candidates*; nothing is uploaded automatically.

## What the labels mean

- **Possible match:** Google Photos search displayed a related result. This is **not verified backup integrity** or original-quality confirmation.
- **Not found by search:** Google Photos displayed a no-results message. The file could still exist in Google Photos under different metadata.
- **Needs review:** The browser page was unclear, interrupted, or could not be recognized safely.
- **Not checked:** No completed search has been recorded.

## Privacy and safety

- Your login remains in your regular browser, not in PhotoSafe Check.
- The browser companion operates only on Google Photos and your local paired app. It does not upload original photo bytes anywhere.
- The optional staging tool makes **new, separately stored copies**, checking SHA-256 hashes and never overwriting originals or existing staging files.
- CSV reports include local paths and filenames; don't publish them.
- If your source folder is synced to OneDrive or another service, newly staged copies may also sync there and use storage.
- **Do not delete any originals on the basis of PhotoSafe Check results.**

## Build from source instead

Install the .NET 10 SDK. In the folder containing `PhotoSafeCheck/PhotoSafeCheck.csproj` run:

```bash
dotnet restore PhotoSafeCheck/PhotoSafeCheck.csproj
dotnet build PhotoSafeCheck/PhotoSafeCheck.csproj
dotnet run --project PhotoSafeCheck/PhotoSafeCheck.csproj
```

You do **not** need Playwright. To build a self-contained version for your own computer, consult `RELEASE_CHECKLIST.md`.

## If something fails

- **Extension can't connect:** ensure the desktop app is open; copy a fresh pairing key; reload the extension and Google Photos tab.
- **No actual search starts:** in the extension popup, click **Use this Photos tab**, then start the scan in the desktop app.
- **Only Needs review results:** Google Photos may have changed its layout. Use **Copy page diagnostics** from the extension popup; it excludes photo names and images. Don't treat these as missing photos.
- **Preview doesn't load:** RAW, HEIC, video or very large images may not have a built-in preview. The result row and full local path still work.
- **Large library looks truncated:** the on-screen list shows up to 600 matches for speed. Use the filename filter or CSV export to inspect the complete inventory.
- **Final browser build fails on Mac/Linux:** keep the build log and report it; these platforms require real verification before formal release.

### If the extension says Desktop connection offline
The desktop app must be open and its local bridge must say READY. Use **Test desktop bridge** and **Retry connection** in the desktop UI. Reload the extension if necessary. If pairing fails, copy the key again from the current desktop installation (portable folders can have different keys).
