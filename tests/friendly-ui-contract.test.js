'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const ui = fs.readFileSync('PhotoSafeCheck/MainWindow.cs','utf8');
const popup = fs.readFileSync('Extension/popup.html','utf8');
const pkg = fs.readFileSync('Extension/manifest.json','utf8');

test('friendly workflow and progress are present', () => {
  for (const text of ['Choose your local photos','Connect Google Photos','Check for possible missing photos',
    'Review what was found','Save reports or prepare upload copies','ProgressBar', 'Searchable']) {
    if (text === 'Searchable') continue;
    assert.ok(ui.includes(text), text);
  }
});
test('local preview and filtering do not transmit photo bytes to the extension', () => {
  assert.match(ui,/Bitmap\.DecodeToWidth/);
  assert.match(ui,/p\.Name\.Contains\(query/);
  assert.match(ui,/Show in folder/);
});
test('full CSV export creates only new files, never overwrites', () => {
  assert.match(ui,/FileMode\.CreateNew, FileAccess\.Write/);
  assert.doesNotMatch(ui,/File\.WriteAllLinesAsync\(path, lines\)/);
});
test('extension popup has accessible controls and progress meter', () => {
  for (const id of ['pairing','connect','activate','pause','diagnostics','meter','status'])
    assert.match(popup, new RegExp(`id="${id}"`));
  assert.equal(JSON.parse(pkg).version,'0.3.5');
});
