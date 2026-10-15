# PhotoSafe Check — GitHub builds and downloadable releases

The GitHub Actions workflow is `.github/workflows/build.yml`. The **desktop application download contains only one runnable executable, inside its own `PhotoSafeCheck/` folder**. The companion Chrome/Edge extension is a **separate** release asset named `PhotoSafeCheck-extension.zip`.

## What users download

| Platform | Release archive | Contents after extraction |
| --- | --- | --- |
| Windows x64 | `PhotoSafeCheck-win-x64-portable.zip` | `PhotoSafeCheck/PhotoSafeCheck.exe` |
| Linux x64 | `PhotoSafeCheck-linux-x64-portable.tar.gz` | `PhotoSafeCheck/PhotoSafeCheck` |
| macOS Intel | `PhotoSafeCheck-osx-x64-portable.tar.gz` | `PhotoSafeCheck/PhotoSafeCheck` |
| macOS Apple Silicon | `PhotoSafeCheck-osx-arm64-portable.tar.gz` | `PhotoSafeCheck/PhotoSafeCheck` |

Each archive has a separate SHA-256 checksum. Windows uses a ZIP; Linux/macOS use tar.gz to retain execute permissions. **No README files, image assets, browser extension files, logs, settings, state, or download-link JSON are placed in the app archive.** The icons, chime, and built-in GitHub download link are embedded in the executable. The app is self-contained: .NET is included in the binary.

The extension is installed separately, typically from Chrome Web Store or `PhotoSafeCheck-extension.zip` from GitHub Releases. This allows the desktop bundle to stay clean.

## First run and storage

On first launch, users choose **Portable** (suggested) or **Local**. Portable creates `PhotoSafeCheck/Data/` next to the executable and keeps its scan history, pairing token, and logs there. Local keeps application state in the user's OS application-data directory. No state folder is included in downloads. See `STORAGE_MODES.md`.

A self-contained single-file .NET application can still extract native libraries into a runtime-managed temporary cache. This is separate from app settings and is an upstream .NET runtime behavior; the user-facing download itself contains only the EXE (or platform binary).

## Build on GitHub

1. Commit the source and `.github/workflows/build.yml` to GitHub.
2. Open **Actions → Build PhotoSafe Check → Run workflow**.
3. Choose `all`, `windows`, `linux`, or `macos`. Leave `after_build=artifacts_only` for test builds.
4. Download artifacts from the completed run. **GitHub wraps each artifact in an additional ZIP**: unzip that first to obtain the release `.zip`/`.tar.gz`, then unpack that archive into the final `PhotoSafeCheck/` folder.

Pushes to `main`/`master` and pull requests build all targets. The workflow uses GitHub-hosted runners matching the target OS and CPU architecture, installs .NET 10, runs extension tests, publishes self-contained single files, executes `--verify-bundle` to check embedded assets without opening the GUI, then packages the clean app archive. The release step also includes the separate extension ZIP.

## Draft releases

**Manual:** Actions → Build PhotoSafe Check → Run workflow → `platform=all` → `after_build=draft_release` → supply an unused `release_tag` (for example `v0.3.7`). This runs the matrix and creates a draft GitHub Release without publishing it.

**Tag:** Commit source first, then push `git tag v0.3.7` and `git push origin v0.3.7`. A successful full build creates a draft release. In either case, review the four app archives and separate extension ZIP under **Releases → Drafts** before publishing.

The workflow currently uses Node.js 24-compatible action versions: `checkout@v5`, `setup-dotnet@v5`, `setup-node@v5`, `setup-python@v6`, `upload-artifact@v6` and `download-artifact@v7`. Extension tests use Node.js 22 independently.

## Release checklist

- Inspect the Windows ZIP: the **only entry** is `PhotoSafeCheck/PhotoSafeCheck.exe`.
- Inspect Linux/macOS tarballs: their **only entry** is `PhotoSafeCheck/PhotoSafeCheck`, and it is executable.
- Confirm the separate `PhotoSafeCheck-extension.zip` has `manifest.json` at its root.
- Test first launch, both storage modes, extension pairing, scan, and final report on real machines.
- Confirm Portable creates Data next to the executable and no PhotoSafe Check state under AppData.
- Confirm Local mode creates user-profile app state as selected.
- Verify that a `v*` release is a draft until manually published.
- Unsigned Windows builds may show SmartScreen warnings; unsigned macOS binaries may be stopped by Gatekeeper. Signing/notarization and testing remain release responsibilities.

## Local build

From the project root (requires .NET 10 SDK and Python 3):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-portable.ps1 -Rid win-x64
```

The output directory `out/PhotoSafeCheck-win-x64-portable/PhotoSafeCheck/` contains only `PhotoSafeCheck.exe`. It is not code-signed. For other targets use the matching runner/platform.
