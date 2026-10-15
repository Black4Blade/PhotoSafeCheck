# Windows GUI-only release fix

Previously the `.csproj` used `OutputType=Exe` for startup troubleshooting, which causes a terminal window to appear alongside the app when users launch the Windows GitHub Release executable.

The project now retains `Exe` for normal development builds, but selects `WinExe` when publishing a `Release` build for `win-x64`. The existing GitHub workflow already invokes `dotnet publish --configuration Release --runtime win-x64` so no special publishing command is needed.

The CI verifier reads the *actual* Windows PE header of the single-file release executable and requires subsystem `2` (`Windows GUI`) instead of `3` (`Windows CUI`). It also retains the embedded-assets self-check. Windows errors remain available through the application's startup log and error message box.

## Rebuild on GitHub

1. Replace the files from the patch ZIP (or use the complete updated source), commit, and push.
2. Run **Actions → Build PhotoSafe Check → Run workflow**, selecting `windows` or `all`.
3. Download the newly generated Windows artifact, unpack its nested archives, and start `PhotoSafeCheck/PhotoSafeCheck.exe`.
4. For a new downloadable GitHub Release, choose `all`, `draft_release`, and a **new** version tag. Existing published release assets do not update automatically.

Nothing changes about Portable vs Local storage mode. An executable-only Windows ZIP still contains exactly `PhotoSafeCheck/PhotoSafeCheck.exe`; Portable creates its `Data/` folder after launch.

This update does not change the browser extension or users' photos. No photo deletion is ever performed.
