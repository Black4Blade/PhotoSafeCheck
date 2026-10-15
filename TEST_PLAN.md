# PhotoSafe Check v0.3 — acceptance test checklist

Use **throwaway test media**. Never decide whether to delete any original photos based on this prototype.

## Build and browser

- Windows: `dotnet restore`, `dotnet build`, `dotnet run` from `PhotoSafeCheck/`.
- Linux and macOS: same commands, with .NET 10 SDK installed.
- Chrome/Edge: load unpacked `Extension/` and refresh the Google Photos tab.
- Check that `node --test tests/*.test.js` passes (29 browser/UI contract tests).

## Results and separate CSV reports

1. Index a small folder with one file known to show in Google Photos, one known to show **No results**, and one uncertain/unsearchable file.
2. Confirm the app classifies known positive as **Possible match** (not guaranteed backup) and explicit negative as **Not found by search**.
3. Pause scanning, then click **Export separate found / not-found CSVs…**.
4. Verify `PhotoSafe_Found_Possible_*.csv` and `PhotoSafe_NotFound_BySearch_*.csv` exist as separate files; if any review results exist verify `PhotoSafe_NeedsReview_*.csv`.
5. Repeat export; older CSV files must remain unchanged and new names must be used.
6. Open the CSV in a spreadsheet program; verify paths, status, timestamps, and any suspicious filenames are treated as text, not formulas.

## Safe copy into scanned root

1. Confirm the copy dialog gives count/size, target folder and safety warning.
2. Confirm only **Not found by search** entries are copied into `<indexed root>/PhotoSafeCheck_NotFound_ToUpload/`.
3. Verify nested relative directories are preserved, e.g. `Trips/2022/a.jpg` => staging `Trips/2022/a.jpg`.
4. Compare original and copy SHA-256 hashes; both must be identical.
5. Run copy again; existing copied media must not be overwritten or modified; existing destination count should increase.
6. Intentionally change an indexed original BEFORE copying; app should skip it and suggest re-indexing.
7. Test cancellation while copying large test files; originals remain intact; any partial copies are labeled `.photosafe-partial-*`.
8. Re-index the same root: the staging folder's copies must not appear as additional indexed photos.
9. If testing symbolic links, the app must refuse unsafe linked source/destination paths.
10. On a OneDrive-synced test folder, consider that new staging copies may sync to OneDrive. Don't use real valuable data to test.

## Browser timer

1. Set 12-second delay and start two or more searches.
2. After each **completed** result, the Google Photos corner badge must show approximately `Next search in 0m 12s` and count toward zero once per second.
3. Popup should also show delay, remaining distinct searches and ETA.
4. Final result should complete promptly **without** another 12-second wait.
5. On rate limits, Google access warnings or repeated uncertain results, scanning should slow or pause. Do not bypass warnings.

## Privacy and safety

- Desktop and extension must not upload media files or Google credentials.
- No source photo is deleted, moved, renamed, edited or overwritten in any scenario.
- CSV export writes new files; staged copying writes new media only.
- Deletion or manual Google Photos upload remains **exclusively a user action outside the app**.

## Friendly v0.3 acceptance tests

1. **First launch:** the left checklist explains the order without showing the private pairing key as plaintext; the app does not claim account access until the extension actually polls it.
2. **Select/index:** choose a disposable nested folder of `.png`, `.jpg` and a video. Verify total count and check that indexing neither creates nor alters source media.
3. **Search and preview:** filter by partial name, select a PNG/JPG and inspect preview, select an unsupported video and verify the app stays responsive. **Show in folder** opens the containing folder, not a delete/move action.
4. **Summary:** verify total, possible, not-found, needs-review, checked-percent and estimated time change as reports arrive. Find two images with identical stems and different extensions to test cautious matching.
5. **Extension:** confirm popup progress bar, 12-second post-result browser corner timer, final search with no trailing delay, and pairing from a normal signed-in Chrome/Edge session.
6. **Exports:** test separate lists for each outcome, filterable onscreen list, and saving a full CSV to a name that already exists. The existing CSV must remain unchanged.
7. **Staging:** copy not-found disposable files into the dedicated nested staging folder. Verify originals remain identical, subfolders are preserved, existing targets skipped, and staging excluded from re-index.
8. **Reset controls:** use Advanced > Clear last result and Clear all results. Scan statuses reset; originals and manual-upload copies stay untouched.
9. **Large screens / small screens:** check a 960x680 window and a 4K scaled display, including long folder names. Screen content must remain scrollable.
10. **Cross-platform:** test Windows x64, Ubuntu x64, Intel macOS and Apple Silicon macOS using the CI preview artifacts.

The friendly preview has no signed installers or formal release guarantee yet.

## v0.3.6 storage tests

1. On clean Windows/Linux/macOS user profile start a fresh unpacked app; verify first-run chooser appears and AppData has not been created yet.
2. Choose Portable with Remember enabled; quit. Verify `App/Data` contains state/pairing/logs and the OS AppData folder was not created. Relaunch and verify no prompt.
3. Choose Local in a separate fresh app directory; verify progress goes to user app data. Relaunch and confirm progress/pairing persist.
4. Uncheck Remember, quit and relaunch twice; confirm chooser always appears without overwriting history.
5. Use Advanced → Storage mode and cleanup to request switching. Relaunch; choose other mode with carry-over. Verify copied state/token match and existing files in destination remain unchanged.
6. In Portable mode use the legacy AppData cleanup preview; cancel and verify no changes, then confirm with disposable test data and verify only listed metadata is removed. Check unknown files and staged images are left alone.
7. Try portable mode from a read-only app directory; confirm an understandable error and Local choice remains available.
8. Restart the extension after a mode switch. Verify the carried-over pairing key works or re-pair explicitly.
9. Test startup exceptions before selection: console shows error and no new AppData log is created.
