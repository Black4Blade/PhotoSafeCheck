'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const ui = fs.readFileSync('PhotoSafeCheck/MainWindow.cs','utf8');
const config = JSON.parse(fs.readFileSync('PhotoSafeCheck/ExtensionLinks.json','utf8'));
const csproj = fs.readFileSync('PhotoSafeCheck/PhotoSafeCheck.csproj','utf8');
const linkCode = fs.readFileSync('PhotoSafeCheck/ExtensionDistributionLinks.cs','utf8');
const pack = fs.readFileSync('scripts/package_preview.py','utf8');
test('app offers two distinct extension installation options and local fallback', () => {
  for (const label of ['Get from Chrome Web Store', 'Download from GitHub Releases', 'Open extension folder (source builds)'])
    assert.ok(ui.includes(label), label);
  assert.ok(ui.includes('HasChromeWebStore') && ui.includes('HasGitHubReleases'));
  assert.ok(ui.includes('Load unpacked'));
});
test('unpublished links are disabled in source and built-in defaults are embedded', () => {
  assert.equal(config.chromeWebStoreUrl,'');
  assert.equal(config.githubReleasesUrl,'');
  assert.match(csproj,/EmbeddedResource Include="ExtensionLinks.json"/);
  assert.match(pack,/shutil.copy2\(source, target\)/);
  assert.match(linkCode,/BundledAssets.OpenLinks/);
});
test('link validator restricts to HTTPS known distribution locations', () => {
  assert.match(linkCode,/UriSchemeHttps/);
  assert.match(linkCode,/chromewebstore.google.com/);
  assert.match(linkCode,/github.com/);
  assert.match(linkCode,/releases/);
});
test('store ZIP packager places manifest at ZIP root', () => {
  const script=fs.readFileSync('scripts/package_extension.py','utf8');
  assert.match(script,/file.relative_to\(root\)/);
  assert.match(script,/manifest.json/);
});
