## v0.3.6 storage acceptance

- [ ] First run prompts for Portable or Local, including when packaged with `portable.mode`.
- [ ] Portable mode on a clean machine creates no PhotoSafe Check AppData folder.
- [ ] Local mode preserves existing scan history and pairing.
- [ ] Remember on/off works across restarts.
- [ ] Switch mode from Advanced; carry-over never overwrites destination files.
- [ ] AppData cleanup asks confirmation, affects only listed PhotoSafe Check metadata and never images.
- [ ] Moving portable directory works if original photo paths still exist.

# Portable release-specific gates (v0.3.5 preview)

- [ ] Confirm the Windows app actually launches (v0.3.2 failed before entering the Avalonia UI event loop).
- [ ] Run with packaged `App/portable.mode` and verify `App/Data/state.json` and `App/Data/logs/startup.log` are created.
- [ ] Verify a **source development run** still reads existing `%LOCALAPPDATA%/PhotoSafeCheck/state.json`.
- [ ] Verify the published self-contained executable works on a test machine **without .NET installed**.
- [ ] Test Linux/macOS executables on real machines (including executable permissions, Gatekeeper/codesigning notices, native libraries).
- [ ] Verify extension connection, pairing, 12-second delay, chime, search results, CSV exports, and optional safe staging copies.
- [ ] Confirm moving the *entire* portable folder preserves progress, while local source paths remain valid or are re-selected.
- [ ] Confirm the release archive contains no user `Data/`, token, logs, image bytes, or saved search results.
- [ ] Do not declare this stable until the above checks pass.

---

# Release checklist — before calling v0.3 stable

**Current status: friendly preview, not a verified production release.**

## Essential tests

- [ ] .NET 10 `dotnet build -c Release` passes on Windows, Linux, macOS.
- [ ] GitHub preview artifacts start on Windows, Linux x64, macOS Intel and Apple Silicon.
- [ ] Choose a folder, index, close/reopen and confirm progress is preserved.
- [ ] Pair extension using normal Chrome and Edge sign-in. Confirm no Playwright is invoked.
- [ ] Verify a known existing image, a known not-found image, and an uncertain search.
- [ ] Confirm an image appearing twice in Google Photos isn't wrongly presented as two verified backups.
- [ ] Check user-visible timer starts after the search, waits 12 seconds, and skips delay after the final search.
- [ ] Test pause/resume, daily limit 500, unlimited setting, and access-warning pause.
- [ ] Verify local preview of .jpg, .png, .gif, and graceful unsupported-video behavior.
- [ ] Verify UI with hundreds/thousands of files and long filenames, different screen sizes, and Windows display scaling.
- [ ] Export all three CSV lists, including formula-like filenames, and ensure no file is overwritten.
- [ ] Stage known not-found photos on a disposable folder; verify SHA-256, cancellation, existing destination collisions, and re-index staging exclusion.
- [ ] Confirm local originals' hashes are unchanged before/after tests.
- [ ] Confirm app and extension never contain delete, rename, move, overwrite-existing-photo or automatic-upload actions.
- [ ] Test UI when no folder, no results, malformed settings, disconnected extension and expired sign-in.
- [ ] Confirm macOS Gatekeeper/distribution experience and Linux executable permissions/dependencies.

## Before public sharing

- [ ] Add a privacy notice and support contact.
- [ ] Run a dependency security audit (`dotnet list package --vulnerable --include-transitive`).
- [ ] Decide on Windows signing and macOS signing/notarization; unsigned builds should be clearly labeled.
- [ ] Create human-readable release notes and a change log; include the extension as part of every preview artifact.
- [ ] Never claim that a Google Photos search proves byte-for-byte backup integrity.
- [ ] No deletion functionality, including in future playful editions.

## Later, separate project milestone

After the safe, friendly release is tested and accepted, make a **Fun edition** using optional cosmetics such as themes, playful success animations, customized completion sounds, achievements and mascots. Keep scan accuracy, pacing, reports, privacy and the no-deletion rule identical to the stable edition.

## v0.3.5 connection checks
- [ ] The desktop bridge reports READY when launched in a portable folder.
- [ ] **Test desktop bridge** succeeds with the same application instance.
- [ ] Companion popup shows **Desktop connected** after pairing, and **Pairing needs updating** on a stale key.
- [ ] Closing the desktop leads to **Desktop connection offline** in the extension, then reopening/retrying recovers.
- [ ] App icon and extension icons are visible on all supported platforms.
