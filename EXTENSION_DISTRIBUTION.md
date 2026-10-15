# Publishing the PhotoSafe Check companion extension

The app's **Connect Google Photos** step now offers two installer options:
**Chrome Web Store** (recommended) and **GitHub Releases** (manual unpacked install).
Both are intentionally disabled until you configure **real** public URLs.
The bundled `Extension/` folder remains available for local installs.

## Before your first publication

- Test the exact extension ZIP in Chrome/Edge. A new browser extension should be reviewed
  for permissions, privacy statement, and user data disclosures before publication.
- Generate the Chrome Web Store upload ZIP with:
  `python scripts/package_extension.py --dest PhotoSafeCheck-extension-v0.3.4.zip`
- The ZIP contains `manifest.json` **at the ZIP root**, as the Chrome Web Store requires.
  Submit through the Chrome Developer Dashboard. Browser store review may take time.
- For GitHub, attach this same ZIP to your project's **Releases** page as an asset.
  A GitHub ZIP requires **extracting**, opening `chrome://extensions` or
  `edge://extensions`, turning on Developer mode, and **Load unpacked** on the
  extracted folder. GitHub release assets are not automatic extension installs.

## Activate download buttons in the desktop application

Once both public URLs exist, open `App/ExtensionLinks.json` in the portable build,
or `PhotoSafeCheck/ExtensionLinks.json` in source *before publishing*.
Example (replace placeholders; these are NOT real URLs):

```json
{
  "chromeWebStoreUrl": "https://chromewebstore.google.com/detail/your-listing/your-extension-id",
  "githubReleasesUrl": "https://github.com/your-account/your-project/releases"
}
```

The next time PhotoSafe Check opens, each valid configured button becomes enabled.
An empty/invalid link remains disabled and displays an upcoming-links hint.
URLs must be HTTPS and match Chrome Web Store / GitHub Releases domains and paths.
You can distribute only the store link, only the GitHub link, or both.
Note: the app reads the config from the executable directory; don't put it in Data.

## Release hygiene

The **desktop app** has its own platform-specific portable ZIPs. The **extension** has
its own extension ZIP. Include the portable app, extension asset, checksums,
setup guide and a link to the Chrome Web Store on each public GitHub release.
Never upload a used `App/Data` folder: it contains local scan history and a pairing token.

The extension is read-only with respect to Google Photos; the application only reports
matches and can create copies of suspected missing *local* files on explicit request.
It never deletes or modifies original photos.
