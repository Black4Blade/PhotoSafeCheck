const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const sandbox = {};
vm.runInNewContext(fs.readFileSync('Extension/search_ui.js', 'utf8'), sandbox);
vm.runInNewContext(fs.readFileSync('Extension/analyzer.js', 'utf8'), sandbox);
const { inspect } = sandbox.PhotoSafeAnalyzer;
const { sameQuery, isOpaqueRoute } = sandbox.PhotoSafeSearchUi;
function page(overrides = {}) {
  return { text: 'Google Photos', tileCount: 0, pathname: '/search/foo',
    expectedQuery: '"foo"', actualQuery: 'foo', querySource: 'visible search field', ...overrides };
}
test('explicit no results is not proof of missing', () => {
  const r = inspect(page({ text: 'No results found' }));
  assert.equal(r.status, 'Not found by search');
  assert.match(r.detail, /does not prove/i);
});
test('visible tiles are only possible matches', () => {
  const r = inspect(page({ tileCount: 2 }));
  assert.equal(r.status, 'Possible match');
});
test('access warning immediately pauses', () => {
  const r = inspect(page({ text: 'Unusual traffic has been detected', tileCount: 20 }));
  assert.equal(r.status, 'Paused');
});
test('unrecognized page cannot claim missing', () => {
  const r = inspect(page());
  assert.equal(r.status, 'Needs review');
});
test('not on search page is needs review', () => {
  const r = inspect(page({ pathname: '/photos' }));
  assert.equal(r.status, 'Needs review');
});
test('filename including underscores remains exact, minus extension', () => {
  const q = '"red_sleepy_keyboard_loop"';
  const r = inspect(page({ text: 'Search results', tileCount: 3,
    pathname: '/search/ChIcabc123abc123abc123abc123abc123', expectedQuery: q,
    actualQuery: 'red_sleepy_keyboard_loop' }));
  assert.equal(r.status, 'Possible match');
  assert.match(r.detail, /red_sleepy_keyboard_loop/);
});
test('actual visible query missing words is needs review', () => {
  const r = inspect(page({ tileCount: 2,
    pathname: '/search/ChIcabc123abc123abc123abc123abc123',
    expectedQuery: '"red_sleepy_keyboard_loop"', actualQuery: 'redkeyboard_loop' }));
  assert.equal(r.status, 'Needs review');
  assert.match(r.detail, /Search text differs/);
});
test('only surrounding quotes may differ, not underscores', () => {
  assert.equal(sameQuery('"red_sleepy_keyboard_loop"', 'red_sleepy_keyboard_loop'), true);
  assert.equal(sameQuery('"red_sleepy_keyboard_loop"', 'redkeyboard_loop'), false);
});
test('opaque Photos URLs are not filename changes', () => {
  assert.equal(isOpaqueRoute('/search/ChIcmRiZWZzSVZ...'), false);
  assert.equal(isOpaqueRoute('/search/ChIcmRiZWZzSVZtcm9wZXJsZW5ndGhzZXJpZXM='), true);
  assert.equal(isOpaqueRoute('/search/ChIcmRiZWZzSVZtcm9wZXJsZW5ndGhzZXJpZXM%3D'), true);
  assert.equal(isOpaqueRoute('/search/%22red_sleepy_keyboard_loop%22'), false);
});
test('unknown opaque URL without visible query is not a possible match', () => {
  const r = inspect(page({ tileCount: 4,
    pathname: '/search/ChIcmRiZWZzSVZtcm9wZXJsZW5ndGhzZXJpZXM=',
    expectedQuery: '"Firefly_RemoveBackground"', actualQuery: '',
    querySource: 'opaque Google Photos URL; search field unavailable' }));
  assert.equal(r.status, 'Needs review');
  assert.match(r.detail, /Cannot confirm/);
});
test('known found photo on opaque URL is possible match when displayed query matches', () => {
  const r = inspect(page({ tileCount: 1,
    pathname: '/search/ChIcmRiZWZzSVZtcm9wZXJsZW5ndGhzZXJpZXM=',
    expectedQuery: '"Firefly_RemoveBackground"',
    actualQuery: 'Firefly_RemoveBackground' }));
  assert.equal(r.status, 'Possible match');
});
test('unknown no-results layout is Needs review', () => {
  const r = inspect(page({ text: 'Google Photos', tileCount: 0,
    source: 'visible-page-thumbnails', loadSeconds: 17 }));
  assert.equal(r.status, 'Needs review');
  assert.match(r.detail, /tiles=0/);
});

test('Google Photos standalone No results is a search miss', () => {
  const r = inspect(page({ text: 'Google Photos\nNo results\nTry a synonym or more general keyword', tileCount: 0 }));
  assert.equal(r.status, 'Not found by search');
});
test('duplicate tiles yield one possible result but never confirmed backup', () => {
  const r = inspect(page({ text: 'Most relevant to your search\nTuesday', tileCount: 1,
    tileStats: { rawTiles: 2, duplicates: 1 } }));
  assert.equal(r.status, 'Possible match');
  assert.match(r.detail, /1 distinct photo-like/);
  assert.match(r.detail, /duplicate display tile/);
  assert.match(r.detail, /NOT an original-file verification/);
});
test('contradictory no results and thumbnails must be reviewed', () => {
  const r = inspect(page({ text: 'No results', tileCount: 1 }));
  assert.equal(r.status, 'Needs review');
});
