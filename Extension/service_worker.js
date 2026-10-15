'use strict';

const BASE = 'http://127.0.0.1:37541';

function safeClient(value) {
  return typeof value === 'string' && /^[a-zA-Z0-9_-]{1,100}$/.test(value);
}

async function bridge(path, method = 'GET', body = undefined) {
  const { token } = await chrome.storage.local.get('token');
  if (!token || !/^[a-f0-9]{48}$/.test(token))
    throw new Error('Pair with the desktop application first.');
  let response;
  try {
    response = await fetch(BASE + path, {
    method,
    headers: {
      'X-PhotoSafe-Token': token,
      ...(body ? { 'Content-Type': 'application/json' } : {})
    },
    ...(body ? { body: JSON.stringify(body) } : {}),
    cache: 'no-store',
    signal: AbortSignal.timeout(15000)
    });
  } catch (error) {
    if (error?.name === 'TimeoutError' || error?.name === 'AbortError')
      throw new Error('Desktop bridge timed out on 127.0.0.1:37541. Check the desktop connection indicator.');
    throw new Error('Desktop bridge unreachable on 127.0.0.1:37541. Open the desktop app, check its connection indicator, and use Retry connection.');
  }
  if (response.status === 401) throw new Error('Pairing key rejected. Copy the CURRENT key from the desktop app and connect again.');
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(payload.error || `Desktop bridge returned HTTP ${response.status}`);
  return payload;
}

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  (async () => {
    if (!message || typeof message.type !== 'string')
      throw new Error('Invalid extension message.');
    if (message.type === 'STATUS') return bridge('/api/status');
    if (message.type === 'JOB' || message.type === 'REPORT') {
      if (!sender.tab?.id || !sender.tab.url?.startsWith('https://photos.google.com/'))
        throw new Error('Only the selected Google Photos tab may scan.');
      const options = await chrome.storage.local.get(['enabled', 'activeTabId']);
      if (!options.enabled || options.activeTabId !== sender.tab.id)
        return { state: 'extension-paused', message: 'This is not the active scanning tab.' };
      if (!safeClient(message.client)) throw new Error('Invalid client ID.');
      const q = '?client=' + encodeURIComponent(message.client);
      if (message.type === 'JOB') return bridge('/api/job' + q);
      const r = message.report;
      if (!r || typeof r.id !== 'string' || !/^[a-f0-9]{32}$/.test(r.id) ||
          !['Possible match', 'Not found by search', 'Needs review', 'Paused'].includes(r.status) ||
          typeof r.detail !== 'string' || r.detail.length > 2000)
        throw new Error('Invalid scan report.');
      return bridge('/api/report' + q, 'POST', r);
    }
    throw new Error('Unsupported message type.');
  })().then(value => respond({ ok: true, data: value }))
    .catch(error => respond({ ok: false, error: error.message }));
  return true;
});
