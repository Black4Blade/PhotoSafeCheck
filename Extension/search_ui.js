// Search UI helpers. Runs in the normal signed-in Google Photos tab.
// No cookies, private APIs, or photo content are accessed.
(function (global) {
  'use strict';
  function unquote(value) {
    return String(value || '').trim().replace(/^\s*["“]|["”]\s*$/g, '').trim();
  }
  function sameQuery(expected, observed) {
    // Ignore ONLY surrounding quotes and spaces: underscores, words and letters
    // must remain unchanged. Browser-side rewriting is not a valid match.
    return unquote(expected) === unquote(observed);
  }
  function displayQuery(value) {
    return String(value || '').trim().slice(0, 250);
  }
  function isOpaqueRoute(path) {
    // Modern Photos search routes can become opaque encoded IDs (ChI.../CA...).
    // Such IDs cannot be used as proof that Google edited the search text.
    let raw = String(path || '').replace(/^\/search\//, '');
    try { raw = decodeURIComponent(raw); } catch { /* Leave unreadable paths unchanged. */ }
    return /^ChI[A-Za-z0-9_+=/-]{20,}$/.test(raw) || /^CA[A-Za-z0-9_+=/-]{30,}$/.test(raw);
  }
  function visible(node) {
    if (!node || !node.isConnected || node.disabled) return false;
    const rect = node.getBoundingClientRect();
    if (rect.width < 70 || rect.height < 10) return false;
    if (node.closest('#__photosafe_status, [aria-hidden="true"]')) return false;
    const style = getComputedStyle(node);
    return style.display !== 'none' && style.visibility !== 'hidden';
  }
  function candidates() {
    // Generic accessibility hints; Google may change its DOM at any time.
    const selector = 'input[type="search"], input[role="searchbox"], [role="searchbox"], ' +
      'input[aria-label], textarea[aria-label], [contenteditable="true"], input[placeholder], [role="combobox"]';
    return [...document.querySelectorAll(selector)].filter(visible).map(node => {
      const hints = [node.getAttribute('aria-label'), node.getAttribute('placeholder'),
        node.getAttribute('title'), node.getAttribute('role'), node.getAttribute('name'), node.id,
        node.closest('[role="search"]')?.getAttribute('aria-label')]
        .filter(Boolean).join(' ').toLowerCase();
      let score = 0;
      if (node.matches('input[type="search"], input[role="searchbox"], [role="searchbox"]')) score += 10;
      if (/search|find|photos|rechercher|buscar|suchen|חיפוש|חפש|поиск/i.test(hints)) score += 6;
      if (node.closest('form[role="search"], [role="search"]')) score += 8;
      if (node.getAttribute('role') === 'combobox') score += 1;
      if (node.matches('input, textarea')) score += 2;
      return { node, score };
    }).sort((a, b) => b.score - a.score);
  }
  function locate() {
    const best = candidates()[0];
    return best && best.score >= 6 ? best.node : null;
  }
  function readQuery() {
    const node = locate();
    if (!node) return { found: false, query: '', source: 'search field not identified' };
    const query = 'value' in node ? node.value : node.textContent;
    return { found: true, query: displayQuery(query), source: 'visible search field' };
  }
  function replaceText(node, query) {
    node.focus();
    if (node.matches('input, textarea')) {
      const prototype = node.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
      Object.getOwnPropertyDescriptor(prototype, 'value').set.call(node, query);
      node.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: query }));
      node.dispatchEvent(new Event('change', { bubbles: true }));
    } else if (node.isContentEditable) {
      const selection = window.getSelection();
      const range = document.createRange();
      range.selectNodeContents(node);
      selection.removeAllRanges();
      selection.addRange(range);
      // execCommand is deprecated, but for controlled contenteditable fields it
      // is still supported by Chrome/Edge and integrates with edit listeners.
      if (!document.execCommand('insertText', false, query)) {
        node.textContent = query;
        node.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: query }));
      }
    } else return false;
    return true;
  }
  function submit(query) {
    const node = locate();
    if (!node) return { ok: false, reason: 'Google Photos search field not found' };
    if (!replaceText(node, query)) return { ok: false, reason: 'Search field is not editable' };
    const current = readQuery();
    if (!current.found || !sameQuery(query, current.query))
      return { ok: false, reason: 'Search field changed the filename while typing' };
    // UI-only events; never access Google's private backend APIs.
    const opts = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true };
    node.dispatchEvent(new KeyboardEvent('keydown', opts));
    node.dispatchEvent(new KeyboardEvent('keypress', opts));
    node.dispatchEvent(new KeyboardEvent('keyup', opts));
    // If the site uses a real HTML search form, request submit as well.
    const form = node.closest('form');
    if (form && typeof form.requestSubmit === 'function') {
      try { form.requestSubmit(); } catch { /* The page may use an SPA-only form. */ }
    }
    return { ok: true, reason: 'Entered exact filename in Google Photos search field' };
  }
  global.PhotoSafeSearchUi = { unquote, sameQuery, isOpaqueRoute, readQuery, submit };
})(typeof globalThis === 'object' ? globalThis : this);
