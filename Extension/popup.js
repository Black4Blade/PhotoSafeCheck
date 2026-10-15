'use strict';
const key = document.getElementById('pairing');
const status = document.getElementById('status');
const connectionBox = document.getElementById('connectionBox');
const connectionTitle = document.getElementById('connectionTitle');
const connectionHelp = document.getElementById('connectionHelp');
const retryConnection = document.getElementById('retryConnection');
function setConnection(state, title, hint) {
  connectionBox.dataset.state = state;
  connectionTitle.textContent = title;
  connectionHelp.textContent = hint;
}

const activate = document.getElementById('activate');
const connect = document.getElementById('connect');
const pause = document.getElementById('pause');
const diagnostics = document.getElementById('diagnostics');
const progressArea = document.getElementById('progressArea');
const meter = document.getElementById('meter');
const meterHint = document.getElementById('meterHint');
let photosTab = null;
let lastSnapshot = null;
let lastLoadedAt = 0;
let lastError = '';
function timeText(seconds) {
  const t = Math.max(0, Math.ceil(seconds));
  if (!Number.isFinite(t)) return 'unknown';
  if (t >= 3600) return `${Math.floor(t / 3600)}h ${Math.floor(t % 3600 / 60)}m`;
  return `${Math.floor(t / 60)}m ${String(t % 60).padStart(2, '0')}s`;
}
function renderLive() {
  if (!lastSnapshot) {
    progressArea.hidden = true;
    if (lastError) show(lastError);
    return;
  }
  const state = lastSnapshot;
  progressArea.hidden = false;
  meter.max = Math.max(1, state.totalFiles || 0);
  meter.value = Math.max(0, (state.totalFiles || 0) - (state.notChecked || 0));
  meterHint.textContent = `${meter.value} / ${state.totalFiles || 0} local files checked`;
  const age = Math.max(0, (Date.now() - lastLoadedAt) / 1000);
  const due = state.nextAllowedUtc ? Date.parse(state.nextAllowedUtc) - Date.now() : null;
  const nextLine = !state.enabled ? 'Next search: scanner paused' :
    state.activeFile ? 'Next search: current search in progress' :
    due !== null && due > 0 ? `Next search in: ${timeText(due / 1000)}` :
    state.notChecked ? 'Next search: ready / loading' : 'Next search: complete';
  const eta = state.notChecked ? `~${timeText((state.estimatedSecondsRemaining || 0) - age)} (estimate)` : 'Complete';
  const dailyLimit = state.dailyLimit ? ' / ' + state.dailyLimit : ' / unlimited';
  show(`Desktop: ${state.enabled ? 'scanning enabled' : 'paused'}\n` +
       `Indexed: ${state.totalFiles} • Left: ${state.notChecked}\n` +
       `Distinct searches remaining: ${state.remainingSearches ?? '?'}\n` +
       `Daily searches: ${state.dailyUsed}${dailyLimit}\n` +
       `Delay AFTER each result: ${state.delaySeconds ?? 12}s\n` +
       `${nextLine}\nEstimated time remaining: ${eta}\n` +
       `Extension: ${lastSnapshot.extensionEnabled ? 'active' : 'paused'}\n` + state.message);
}

function show(text) { status.textContent = text; }
function ask(type) {
  return new Promise((resolve, reject) => {
    chrome.runtime.sendMessage({ type }, response => {
      if (chrome.runtime.lastError) { reject(new Error(chrome.runtime.lastError.message)); return; }
      if (!response?.ok) { reject(new Error(response?.error || 'Unknown bridge error')); return; }
      resolve(response.data);
    });
  });
}
async function refresh() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  photosTab = tab?.url?.startsWith('https://photos.google.com/') ? tab : null;
  activate.disabled = !photosTab;
  diagnostics.disabled = !photosTab;
  const { token, enabled, activeTabId } = await chrome.storage.local.get(['token', 'enabled', 'activeTabId']);
  if (!token) {
    lastSnapshot = null;
    lastError = '';
    setConnection('pairing', 'Not paired yet', 'Copy the pairing key from the desktop application and enter it below.');
    show('Waiting for a pairing key.');
    return;
  }
  try {
    const state = await ask('STATUS');
    lastSnapshot = { ...state, extensionEnabled: enabled };
    lastLoadedAt = Date.now();
    lastError = '';
    const tabReady = photosTab && enabled && activeTabId === photosTab.id;
    setConnection('online', 'Desktop connected', tabReady
      ? 'Google Photos tab active. The scanner can run when enabled in the desktop app.'
      : 'Connection is working. Open Google Photos and choose “Use this Photos tab” to scan.');
    renderLive();
  } catch (err) {
    lastSnapshot = null;
    const message = String(err?.message || err);
    const mismatch = /pairing key|rejected|401|invalid/i.test(message);
    setConnection(mismatch ? 'pairing' : 'offline', mismatch ? 'Pairing needs updating' : 'Desktop connection offline',
      mismatch ? 'The portable build may have a different key. Copy the CURRENT key from its window and connect again.'
               : 'Check the desktop app’s connection indicator, click Retry connection there, then retry here.');
    lastError = 'Connection issue: ' + message;
    renderLive();
  }
}
connect.addEventListener('click', async () => {
  const token = key.value.trim().toLowerCase();
  if (!/^[a-f0-9]{48}$/.test(token)) { show('Paste the complete 48-character pairing key.'); return; }
  await chrome.storage.local.set({ token });
  key.value = '';
  await refresh();
});
activate.addEventListener('click', async () => {
  if (!photosTab) { show('Open Google Photos first.'); return; }
  await chrome.storage.local.set({ enabled: true, activeTabId: photosTab.id });
  try { await chrome.tabs.sendMessage(photosTab.id, { type: 'WAKE' }); }
  catch { /* A page refresh injects content script automatically. */ }
  await refresh();
});
pause.addEventListener('click', async () => {
  await chrome.storage.local.set({ enabled: false });
  await refresh();
});
diagnostics.addEventListener('click', async () => {
  if (!photosTab) { show('Open a Google Photos tab first.'); return; }
  try {
    const payload = await chrome.tabs.sendMessage(photosTab.id, { type: 'DIAGNOSTICS' });
    if (!payload?.ok) throw new Error('Extension content script not ready. Refresh Google Photos.');
    await navigator.clipboard.writeText(JSON.stringify(payload.data, null, 2));
    show('Page diagnostics copied. Contains only page-element counts, not filenames, photos or account information.');
  } catch (err) { show('Diagnostics failed: ' + err.message + '. Refresh Google Photos and retry.'); }
});
retryConnection.addEventListener('click', () => refresh().catch(error => { show(error.message); }));
refresh();

// Local-only clock refresh; recheck the desktop every 6 seconds, not Google.
setInterval(renderLive, 1000);
setInterval(() => { refresh().catch(() => {}); }, 6000);
