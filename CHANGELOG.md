## v0.3.6 preview — User-selected storage modes

- Added first-run Portable or Local mode chooser, with remember/ask-on-every-launch option.
- Portable mode writes its settings, pairing and logs into App/Data and does not create AppData on first launch.
- Added safe carry-over of state/pairing on mode switch without overwriting existing destination state.
- Advanced Storage & Privacy shows paths, queues a mode choice for next startup, and offers explicit whitelist-only cleanup of legacy AppData while in Portable mode.
- Scanner, extension, staging and photo-safety rules unchanged.

# v0.3.5 — Browser companion download locations

- Chrome Web Store and GitHub Releases buttons in the desktop Connect step.
- Publisher-managed external links file included in portable bundles, disabled until published.
- Bundled extension folder remains available as a fallback.
- Updated connection help and release docs; store-ready extension zip packager.
- Scanner, extension JS, and no-original-file-mutation rule unchanged.

---

## v0.3.4 — Connection clarity and matching application icons
- Added a shared photo-check logo: Windows app icon, Avalonia sidebar logo, Chrome/Edge toolbar and popup icons (16/32/48/128 px), editable SVG masters.
- Desktop shows bridge offline, ready/waiting, and recent extension connection separately.
- Added desktop Retry connection, Test desktop bridge, and startup logging for connection failures.
- Extension presents explicit pairing/offline/online indicators, troubleshooting instructions, and a manual connection retry.
- Replaced opaque `Failed to fetch` with contextual information pointing to the local service at 127.0.0.1:37541.
- Retained portable behavior, safe copying, scan results, and strict read-only treatment of originals.

## v0.3.3 — Dispatcher startup fix and portable preview

- Register Avalonia `Dispatcher.UIThread.UnhandledException` after framework setup, preventing a prematurely created dispatcher.
- Add explicit portable storage mode (`App/portable.mode`) that keeps user settings, results, pairing token, and logs in `App/Data`.
- Include a self-contained portable packaging command and preservation/migration guide.
- Leave scanner, extension, safe-copy rules, and result confidence classifications unchanged.

## v0.3.2 — Friendly preview startup hotfix

- Fix `Font weight must be > 0` by replacing `FontWeight weight = default` in the text helper with an overload defaulting to `FontWeight.Normal`.
- Keep startup error logging from v0.3.1.
- No changes to browser extension, scanner scheduling, result classifications, export/copy policy or user photo files.

# Changes

## v0.3.1 — Startup diagnostic build
- Desktop project output changed from Windows GUI subsystem to console so `dotnet run` exposes errors.
- Startup exceptions recorded in `%LOCALAPPDATA%\PhotoSafeCheck\logs\startup.log`.
- Main-window construction failures show a visible fallback diagnostic window.
- Initialisation stages are logged (theme and window).
- No photo scanning, matching, export or deletion behavior changes.

# Changelog

## v0.3.0 — Friendly preview (not a formal release)

- Desktop interface redesigned with a persistent setup checklist, progress summary and four result counters.
- Added searching within indexed filenames/folders, filtering by outcome, selected local photo preview and a cross-platform **Show in folder** action.
- Browser companion popup restyled and given a progress meter; search engine, matching behavior, cooldown and countdown preserved from v0.2.6.
- Result reset controls relocated to an Advanced section so first-time users are less likely to clear their progress by mistake.
- Local bridge displays recently observed authenticated companion activity without exposing the pairing token.
- Removed always-visible pairing key; **Copy pairing key** is the intended method.
- Full CSV export now uses `FileMode.CreateNew` and refuses to overwrite an existing file.
- Added cross-platform self-contained preview publishing workflow and nontechnical setup/release checklists.
- Retained v0.2.6 read-only scanner, separate reports, SHA-256-verified optional staging copies, no automatic upload, and no file deletion functions.

## v0.2.6 — Safe export / copy / browser cooldown
- Separate possible-match, not-found and needs-review CSV files.
- Optional manual-upload staging copies, strict no-overwrite behavior.
- Browser next-search countdown starts after each reported search, no trailing delay.
