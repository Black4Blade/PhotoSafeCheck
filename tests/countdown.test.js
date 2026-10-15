const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { webcrypto } = require('node:crypto');

function simulation() {
  let currentTime = Date.parse('2026-10-15T12:00:00Z');
  const scheduled = [];
  const intervals = [];
  const session = new Map();
  let corner;
  let jobs = 0;
  const body = {
    appendChild(node) { corner = node; node.isConnected = true; },
    querySelectorAll() { return []; }, innerText: ''
  };
  class MockDate extends Date { static now() { return currentTime; } }
  const sandbox = {
    crypto: webcrypto, Date: MockDate, URL,
    document: { body, documentElement: {}, createElement() {
      return { style: {}, textContent: '', isConnected: false };
    } },
    location: { href: 'https://photos.google.com/', pathname: '/' },
    sessionStorage: {
      getItem: k => session.get(k) || null,
      setItem: (k, v) => session.set(k, v),
      removeItem: k => session.delete(k)
    },
    setInterval: cb => { intervals.push(cb); return intervals.length; },
    setTimeout: (cb, delay) => { scheduled.push({ cb, delay }); return scheduled.length; },
    clearTimeout() {},
    chrome: {
      storage: { local: { async get() { return { enabled: true }; } } },
      runtime: {
        lastError: null,
        onMessage: { addListener() {} },
        sendMessage(message, cb) {
          if (message.type === 'JOB') {
            jobs++;
            const data = jobs === 1
              ? { state: 'waiting', nextAllowedUtc: new Date(currentTime + 12000).toISOString(),
                  estimatedSecondsRemaining: 65, remainingSearches: 3 }
              : { state: 'complete', message: 'Pass complete' };
            cb({ ok: true, data });
          } else { cb({ ok: true, data: {} }); }
        }
      }
    },
    PhotoSafeSearchUi: { readQuery: () => ({ found: false, query: '' }) },
    PhotoSafeAnalyzer: { inspect: () => ({ status: 'Needs review', detail: '' }) }
  };
  sandbox.globalThis = sandbox;
  vm.runInNewContext(fs.readFileSync('Extension/content.js', 'utf8'), sandbox);
  return {
    async scheduledTick() { const t = scheduled.pop(); assert.ok(t); await t.cb(); },
    advance(seconds) { currentTime += seconds * 1000; intervals.forEach(fn => fn()); },
    get text() { return corner?.textContent || ''; }, get jobs() { return jobs; }
  };
}

test('corner badge shows an actual ticking delay and remaining estimate', async () => {
  const app = simulation();
  await app.scheduledTick();
  assert.match(app.text, /Waiting between searches/);
  assert.match(app.text, /Next search in 0m 12s/);
  assert.match(app.text, /3 searches left/);
  app.advance(5);
  assert.match(app.text, /Next search in 0m 07s/);
  assert.match(app.text, /Approx. 1m 00s remaining/);
  assert.equal(app.jobs, 1, 'countdown updates do not request Google or another job');
});

test('corner badge switches to complete without a countdown after last result', async () => {
  const app = simulation();
  await app.scheduledTick();
  await app.scheduledTick();
  assert.match(app.text, /Pass complete/);
  assert.doesNotMatch(app.text, /Next search in/);
});

test('inter-search cooldown is set on report, not job dispatch', () => {
  const source = fs.readFileSync('PhotoSafeCheck/ScannerCoordinator.cs', 'utf8');
  const report = source.slice(source.indexOf('public bool Report('));
  const issue = source.slice(source.indexOf('public JobResponse NextJob('), source.indexOf('public bool Report('));
  assert.match(report, /nextAllowed = completedAt\.AddSeconds\(state\.DelaySeconds\)/);
  assert.doesNotMatch(issue, /nextAllowed = now\.AddSeconds\(state\.DelaySeconds\)/);
});
