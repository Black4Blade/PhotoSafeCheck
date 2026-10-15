# PhotoSafe Check 📸

**Find photos and videos that might still need backing up to Google Photos.**

PhotoSafe Check helps you compare files on your computer with search results from your Google Photos library. Instead of checking thousands of files by hand, you can see which ones appear to be uploaded and which ones may need your attention.

## Features

- **Scan local folders** for photos and videos.
- **Check Google Photos** through a companion extension for Chrome or Edge.
- **Review clear results:** possible match, not found by search, needs review, or not checked.
- **Pause and resume** scans without losing your progress.
- **Filter, preview, and export** your results.
- **Prepare uploads** by copying files not found by search into a separate folder, ready for you to upload manually.

## Getting started

1. Download a desktop build from [Releases](../../releases) when available, then extract and run it.
2. Choose a folder of photos or videos to check.
3. Install the PhotoSafe Check browser companion from the Chrome Web Store when published, or load the included `Extension` folder in Chrome or Edge.
4. Open [Google Photos](https://photos.google.com/) and sign in normally.
5. Pair the extension with the desktop app, select your Google Photos tab, and start checking.
6. Review the results and decide which files you want to upload.

For more detail, see the [setup guide](SETUP_GUIDE.md).

## Important to know

PhotoSafe Check uses Google Photos search results to identify **possible backup gaps**. A result marked **not found by search** is not proof that the photo is missing; it might still exist under different metadata. Similarly, a possible match does not verify that an identical full-quality file is backed up. Review the results before making decisions about your files.

PhotoSafe Check does not automatically upload your photos. Preparing files for upload creates separate copies of files marked not found by search.

## Build from source

Requires the **.NET 10 SDK**.

```bash
dotnet restore PhotoSafeCheck/PhotoSafeCheck.csproj
dotnet run --project PhotoSafeCheck/PhotoSafeCheck.csproj
```

## Feedback

Found a problem or have an idea? Open a [GitHub issue](../../issues).