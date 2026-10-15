'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const manifest = require('../Extension/manifest.json');
const ui = fs.readFileSync('PhotoSafeCheck/MainWindow.cs', 'utf8');
const worker = fs.readFileSync('Extension/service_worker.js','utf8');
const popup = fs.readFileSync('Extension/popup.html','utf8');

function simulateBridge(fetchMock, token = 'a'.repeat(48)) {
  let listener;
  const chrome = {
    runtime: { onMessage: { addListener(cb){ listener = cb; } } },
    storage: { local: { get: async () => ({token}) } }
  };
  const sandbox = { chrome, fetch: fetchMock, AbortSignal, console, URL };
  vm.runInNewContext(worker, sandbox, {filename:'service_worker.js'});
  return new Promise(resolve => {
    const wasAsync = listener({type:'STATUS'}, {}, data => resolve(data));
    assert.equal(wasAsync, true);
  });
}

test('offline desktop is diagnosed rather than returning opaque Failed to fetch', async () => {
  const result = await simulateBridge(async () => {throw new TypeError('Failed to fetch');});
  assert.equal(result.ok, false);
  assert.match(result.error, /Desktop bridge unreachable/);
  assert.match(result.error, /127\.0\.0\.1:37541/);
  assert.doesNotMatch(result.error, /Failed to fetch/);
});
test('invalid pairing key gives a distinct recovery path', async () => {
  const result = await simulateBridge(async () => ({status:401}));
  assert.equal(result.ok, false);
  assert.match(result.error, /Pairing key rejected/);
});
test('valid bridge status succeeds', async () => {
  const result = await simulateBridge(async () => ({status:200,ok:true,json:async()=>({totalFiles:4})}));
  assert.equal(result.ok, true);
  assert.equal(result.data.totalFiles, 4);
});
test('desktop and extension present explicit connection status and retry actions', () => {
  assert.match(ui,/bridgeIndicator/);
  assert.match(ui,/RestartBridge/);
  assert.match(ui,/TestBridge/);
  assert.match(ui,/bridge\.IsListening/);
  for (const id of ['connectionBox','connectionTitle','connectionHelp','retryConnection'])
    assert.ok(popup.includes('id="'+id+'"'), id);
});
test('matching branded app and extension icons are included in manifest and project', () => {
  const root = require('node:path').resolve('.');
  for (const size of [16,32,48,128]) {
    assert.equal(manifest.icons[size],`icons/icon${size}.png`);
    assert.ok(fs.existsSync(`${root}/Extension/icons/icon${size}.png`));
  }
  for (const path of ['PhotoSafeCheck/Assets/photosafe.png','PhotoSafeCheck/Assets/photosafe.ico',
     'PhotoSafeCheck/Assets/photosafe.svg','Extension/icons/logo.svg'])
    assert.ok(fs.existsSync(`${root}/${path}`), path);
  assert.match(fs.readFileSync('PhotoSafeCheck/PhotoSafeCheck.csproj','utf8'), /ApplicationIcon/);
});
