const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { webcrypto } = require('node:crypto');

// Simulate a search gallery *outside* [role=main], the case the old scanner
// missed. No real browser session, account, network, or media content is used.
function setup({ elements = [], bodyText = 'Google Photos Search' } = {}) {
  const listeners = [];
  const state = new Map();
  const body = {
    innerText: bodyText,
    appendChild() {},
    querySelectorAll(selector) {
      if (selector === 'img') return elements.filter(e => e.tagName === 'IMG');
      if (selector.startsWith('a[href*=')) return elements.filter(e => e.isLink);
      if (selector === '[role="gridcell"]') return elements.filter(e => e.isGrid);
      if (selector === 'canvas') return elements.filter(e => e.tagName === 'CANVAS');
      return elements;
    }
  };
  const document = {
    body,
    documentElement: { lang: 'en' },
    querySelectorAll: selector => body.querySelectorAll(selector),
    createElement: () => ({ style: {}, textContent: '' })
  };
  const sandbox = {
    document,
    location: { pathname: '/search/ChIcverylongidentifiersuchasthis123', href: 'https://photos.google.com/search/ChIcverylongidentifiersuchasthis123' },
    crypto: webcrypto,
    window: { innerHeight: 1000, innerWidth: 1400 },
    sessionStorage: {
      getItem: key => state.get(key) || null,
      setItem: (key, value) => state.set(key, value),
      removeItem: key => state.delete(key)
    },
    chrome: { runtime: { onMessage: { addListener: fn => listeners.push(fn) } } },
    getComputedStyle: e => ({ display: 'block', visibility: 'visible', opacity: '1', backgroundImage: e.backgroundImage || 'none' }),
    URL, setTimeout: () => 1, clearTimeout: () => {}, setInterval: () => 1
  };
  sandbox.globalThis = sandbox;
  for (const name of ['search_ui.js','analyzer.js','content.js']) {
    vm.runInNewContext(fs.readFileSync('Extension/' + name, 'utf8'), sandbox);
  }
  let payload;
  listeners[0]({ type: 'DIAGNOSTICS' }, null, response => { payload = response; });
  return payload.data;
}
function imageNode({ width = 150, height = 130, nav = false, photoHref = '', imageSrc = 'https://lh3.googleusercontent.com/pw/example' } = {}) {
  return {
    tagName: 'IMG', currentSrc: imageSrc,
    src: imageSrc,
    getBoundingClientRect() { return { width, height, left: 12, right: 12+width, top: 200, bottom: 200+height }; },
    closest(query) {
      if (nav && query.includes('header')) return {};
      if (photoHref && query.includes('a[href*=')) return { getAttribute: () => photoHref };
      return null;
    },
    getAttribute: () => null,
    hasAttribute: () => false,
    matches: () => false,
    querySelector: () => null
  };
}
test('find gallery images even without main element', () => {
  const d = setup({ elements: [imageNode({ imageSrc: 'https://lh3.googleusercontent.com/pw/first' }), imageNode({ imageSrc: 'https://lh3.googleusercontent.com/pw/second' })] });
  assert.equal(d.tileCount, 2);
  assert.equal(d.images, 2);
});
test('ignore tiny icon and navigation avatar', () => {
  const d = setup({ elements: [imageNode({width:25,height:25}), imageNode({nav:true})] });
  assert.equal(d.tileCount, 0);
});
test('recognize visible photo tile CSS background', () => {
  const el = imageNode();
  el.tagName = 'DIV'; el.src = ''; el.currentSrc = '';
  el.backgroundImage = 'url("https://lh3.googleusercontent.com/pw/other")';
  const d = setup({ elements: [el] });
  assert.equal(d.tileCount, 1);
  assert.equal(d.backgrounds, 1);
});
test('diagnostics contain no sensitive content', () => {
  const d = setup({ elements: [imageNode()], bodyText: 'secret private filename IMG_1234.JPG' });
  const json = JSON.stringify(d);
  assert.equal(json.includes('secret'), false);
  assert.equal(json.includes('IMG_1234'), false);
  assert.equal(json.includes('googleusercontent'), false);
  assert.equal(typeof d.bodyTextLength, 'number');
});

test('repeated thumbnail in relevance and dated sections counts one distinct photo', () => {
  const same = '/photo/AF1QipSamePhoto98765';
  const d = setup({ elements: [
    imageNode({ photoHref: same, imageSrc: 'https://lh3.googleusercontent.com/pw/first=w200' }),
    imageNode({ photoHref: same, imageSrc: 'https://lh3.googleusercontent.com/pw/first=w400' })
  ] });
  assert.equal(d.rawTiles, 2);
  assert.equal(d.tileCount, 1);
  assert.equal(d.duplicates, 1);
});
test('two different photo identities remain two', () => {
  const d = setup({ elements: [
    imageNode({ photoHref: '/photo/a', imageSrc: 'https://lh3.googleusercontent.com/pw/a' }),
    imageNode({ photoHref: '/photo/b', imageSrc: 'https://lh3.googleusercontent.com/pw/b' })
  ] });
  assert.equal(d.tileCount, 2);
  assert.equal(d.duplicates, 0);
});
test('duplicate Google thumbnail resources count as one if direct links absent', () => {
  const d = setup({ elements: [
    imageNode({ imageSrc: 'https://lh3.googleusercontent.com/pw/123456=w180-h180' }),
    imageNode({ imageSrc: 'https://lh3.googleusercontent.com/pw/123456=w360-h360' })
  ] });
  assert.equal(d.tileCount, 1);
});
