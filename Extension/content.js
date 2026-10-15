'use strict';

// Runs only in photos.google.com. Deliberately uses the browser's normal
// authenticated page. It does not read cookies, tokens, Google APIs or photo bytes.
if (!globalThis.__photoSafeCompanionLoaded) {
  globalThis.__photoSafeCompanionLoaded = true;
  const PENDING_KEY = 'photosafe-pending-v2';
  const CLIENT_KEY = 'photosafe-client-v2';
  const lastClient = sessionStorage.getItem(CLIENT_KEY);
  const client = lastClient && /^[a-f0-9]{32}$/.test(lastClient)
    ? lastClient : [...crypto.getRandomValues(new Uint8Array(16))]
      .map(b => b.toString(16).padStart(2, '0')).join('');
  sessionStorage.setItem(CLIENT_KEY, client);
  let timer = null;
  let busy = false;
  let badge;
  let countdown = null; // UI-only clock; never makes additional Google Photos requests.

  function message(type, fields = {}) {
    return new Promise((resolve, reject) => {
      chrome.runtime.sendMessage({ type, client, ...fields }, response => {
        if (chrome.runtime.lastError) {
          reject(new Error(chrome.runtime.lastError.message)); return;
        }
        if (!response?.ok) { reject(new Error(response?.error || 'Bridge error')); return; }
        resolve(response.data);
      });
    });
  }

  function display(text) {
    if (!badge) {
      badge = document.createElement('div');
      badge.id = '__photosafe_status';
      Object.assign(badge.style, {
        position: 'fixed', right: '14px', bottom: '14px', zIndex: 2147483000,
        background: '#152334', color: '#d8f7ec', border: '1px solid #5ba99b',
        borderRadius: '10px', padding: '9px 13px', font: '12px system-ui,sans-serif',
        maxWidth: '360px', whiteSpace: 'pre-line', lineHeight: '1.45',
        boxShadow: '0 3px 14px #0005', pointerEvents: 'none'
      });
    }
    // Google Photos dynamically rebuilds parts of the DOM; reattach if needed.
    if (!badge.isConnected) (document.body || document.documentElement).appendChild(badge);
    badge.textContent = 'PhotoSafe · ' + text;
  }

  function formatDuration(seconds) {
    if (!Number.isFinite(seconds)) return 'unknown';
    const total = Math.max(0, Math.ceil(seconds));
    if (total >= 3600) return `${Math.floor(total / 3600)}h ${Math.floor(total % 3600 / 60)}m`;
    return `${Math.floor(total / 60)}m ${String(total % 60).padStart(2, '0')}s`;
  }

  function progressLabel() {
    if (!countdown) return '';
    const left = Math.max(0, Math.ceil((countdown.dueAt - Date.now()) / 1000));
    const elapsed = Math.max(0, (Date.now() - countdown.setAt) / 1000);
    const eta = Math.max(0, countdown.eta - elapsed);
    return `Waiting between searches\nNext search in ${formatDuration(left)}\nApprox. ${formatDuration(eta)} remaining · ${countdown.remaining} searches left`;
  }

  // Update just the UI every second, without polling Google Photos.
  setInterval(() => {
    if (countdown) display(progressLabel());
  }, 1000);

  function schedule(milliseconds) {
    if (timer !== null) clearTimeout(timer);
    timer = setTimeout(tick, Math.max(300, Math.min(120000, milliseconds)));
  }

  function storedPending() {
    try {
      const value = JSON.parse(sessionStorage.getItem(PENDING_KEY) || 'null');
      return value?.id && value?.query && value?.client === client ? value : null;
    } catch { return null; }
  }

  // Google Photos uses multiple thumbnail layouts and may render the gallery
  // OUTSIDE [role=main]. Survey the page as a whole, excluding navigation and
  // the extension's own badge. Never request, download, or inspect image bytes.
  // Separate counts of DOM thumbnail tiles and *distinct identities*. Google
  // Photos can repeat one media item under "Most relevant" and under its date.
  // Prefer the photo link's identity; fall back to a Google thumbnail resource
  // identity when the same photo appears as CSS backgrounds. Unknown identities
  // are kept separate rather than merging unrelated photos.
  function mediaIdentity(node) {
    const link = node.matches('a[href*="/photo/"]') ? node :
      node.closest('a[href*="/photo/"]') || node.querySelector('a[href*="/photo/"]');
    const href = link?.getAttribute('href') || '';
    const photoId = href.match(/\/photo\/([^/?#]+)/);
    if (photoId) return 'photo:' + photoId[1];
    const resource = node.currentSrc || node.src ||
      getComputedStyle(node).backgroundImage?.match(/url\(\s*['"]?([^'"\)]+)['"]?\s*\)/i)?.[1] ||
      node.getAttribute('data-latest-bg') || '';
    if (!resource) return null;
    try {
      const url = new URL(resource, location.href);
      // Ignore icons, data URIs, and generic placeholders; never merge on those.
      if (!/(^|\.)googleusercontent\.com$/i.test(url.hostname)) return null;
      const resourcePath = url.pathname.replace(/=([a-z0-9_-]+)$/i, '');
      if (!resourcePath || resourcePath === '/') return null;
      return 'media:' + url.hostname + resourcePath;
    } catch { return null; }
  }

  function tileEvidence() {
    const stats = { images: 0, backgrounds: 0, photoLinks: 0,
      canvases: 0, gridCells: 0, potentialNodes: 0, rejectedSmall: 0,
      rawTiles: 0, duplicates: 0, distinctIdentities: 0 };
    if (!document.body) return { count: 0, source: 'document-not-ready', stats };
    const candidates = document.body.querySelectorAll(
      'img, [role="img"], [style*="background"], [role="gridcell"], ' +
      '[role="listitem"], a[href*="/photo/"], canvas, [data-latest-bg]');
    const tiles = new Map();
    let scanned = 0;
    for (const node of candidates) {
      if (++scanned > 12000) break;
      if (node.closest('header, nav, aside, [role="navigation"], [role="banner"], ' +
            '[role="menu"], [role="toolbar"], #__photosafe_status')) continue;
      const rect = node.getBoundingClientRect();
      if (rect.width < 60 || rect.height < 60 || rect.width > 3200 || rect.height > 3200) {
        stats.rejectedSmall++; continue;
      }
      if (rect.bottom < -100 || rect.top > window.innerHeight + 1100 ||
          rect.right < 0 || rect.left > window.innerWidth + 200) continue;
      const style = getComputedStyle(node);
      if (style.display === 'none' || style.visibility === 'hidden' ||
          Number(style.opacity) === 0) continue;
      const isPhotoLink = node.matches('a[href*="/photo/"]');
      const isImg = node.tagName === 'IMG' &&
        !/\.svg(?:$|\?)/i.test(node.currentSrc || node.src || '') &&
        Boolean(node.currentSrc || node.src);
      const isCanvas = node.tagName === 'CANVAS' && rect.width >= 90 && rect.height >= 90;
      const bg = style.backgroundImage || '';
      const isBackground = /url\s*\(/i.test(bg) && !/\.svg(?:[?"']|\))/i.test(bg);
      const isGrid = node.matches('[role="gridcell"], [role="listitem"]') &&
        rect.width >= 80 && rect.height >= 80 &&
        Boolean(node.querySelector('img, canvas, [role="img"], [style*="background"], a[href*="/photo/"]'));
      const isRoleImage = node.getAttribute('role') === 'img' &&
        (isBackground || Boolean(node.querySelector('img, canvas')));
      const isLoadedBackground = node.hasAttribute('data-latest-bg') &&
        rect.width >= 80 && rect.height >= 80 && Boolean(node.getAttribute('data-latest-bg'));
      if (!(isPhotoLink || isImg || isCanvas || isBackground || isGrid ||
            isRoleImage || isLoadedBackground)) continue;
      if (isPhotoLink) stats.photoLinks++;
      if (isImg) stats.images++;
      if (isBackground || isLoadedBackground) stats.backgrounds++;
      if (isCanvas) stats.canvases++;
      if (isGrid) stats.gridCells++;
      stats.potentialNodes++;
      // Avoid counting several nested DOM nodes as several tiles.
      const tile = node.closest('a[href*="/photo/"], [role="gridcell"], ' +
        '[data-item-id], [role="listitem"], [data-id], [role="button"]') || node;
      if (!tiles.has(tile)) tiles.set(tile, mediaIdentity(node) || mediaIdentity(tile));
      else if (!tiles.get(tile)) tiles.set(tile, mediaIdentity(node) || mediaIdentity(tile));
    }
    const known = new Set();
    let unknown = 0;
    for (const identity of tiles.values()) {
      if (identity) known.add(identity);
      else unknown++;
    }
    stats.rawTiles = tiles.size;
    stats.distinctIdentities = known.size;
    const count = known.size + unknown;
    stats.duplicates = Math.max(0, tiles.size - count);
    return { count, source: 'whole-page-unique-photo-elements', stats };
  }

  // Copyable, privacy-safe diagnostics. Deliberately excludes filename, photo
  // paths/URLs, text content, credentials, and account identifiers.
  function diagnosticsSnapshot() {
    const tiles = tileEvidence();
    const search = PhotoSafeSearchUi.readQuery();
    return {
      version: '0.2.6',
      page: location.pathname.startsWith('/search/') ? 'search' : 'other',
      opaqueSearchRoute: PhotoSafeSearchUi.isOpaqueRoute(location.pathname),
      searchFieldFound: search.found,
      searchFieldHasText: Boolean(search.query),
      pageLanguage: (document.documentElement.lang || 'unknown').slice(0, 20),
      tileCount: tiles.count,
      ...tiles.stats,
      bodyTextLength: (document.body?.innerText || '').length,
      documentImages: document.querySelectorAll('img').length,
      allPhotoLinks: document.querySelectorAll('a[href*="/photo/"]').length,
      allGridCells: document.querySelectorAll('[role="gridcell"]').length,
      allCanvas: document.querySelectorAll('canvas').length
    };
  }

  function inspect(job) {
    const tiles = tileEvidence();
    const text = document.body?.innerText || '';
    const visibleSearch = PhotoSafeSearchUi.readQuery();
    let actualQuery = '';
    let querySource = 'unavailable';
    if (visibleSearch.found && visibleSearch.query) {
      actualQuery = visibleSearch.query;
      querySource = 'visible search field';
    } else if (location.pathname.startsWith('/search/') &&
               !PhotoSafeSearchUi.isOpaqueRoute(location.pathname)) {
      try {
        actualQuery = decodeURIComponent(location.pathname.replace(/^\/search\//, ''));
        querySource = 'literal URL path (search field unavailable)';
      } catch { querySource = 'unreadable search URL'; }
    } else if (PhotoSafeSearchUi.isOpaqueRoute(location.pathname)) {
      querySource = 'opaque Google Photos URL; search field unavailable';
    }
    return PhotoSafeAnalyzer.inspect({
      text, tileCount: tiles.count, source: tiles.source,
      pathname: location.pathname, expectedQuery: job.query,
      actualQuery, querySource,
      loadSeconds: Math.floor((Date.now() - job.started) / 1000),
      tileStats: tiles.stats
    });
  }

  function openSearchUrl(job) {
    // Compatibility fallback if synthetic search field events do not cause Google
    // Photos to navigate. The URL is not assumed to retain the user's query.
    job.method = 'url-fallback';
    job.started = Date.now();
    sessionStorage.setItem(PENDING_KEY, JSON.stringify(job));
    const destination = 'https://photos.google.com/search/' + encodeURIComponent(job.query);
    location.assign(destination);
  }

  async function tick() {
    if (busy) return;
    busy = true;
    try {
      const settings = await chrome.storage.local.get(['enabled', 'activeTabId']);
      if (!settings.enabled) { countdown = null; display('Extension paused'); schedule(10000); return; }
      const pending = storedPending();
      if (pending) {
        countdown = null;
        const elapsed = Date.now() - pending.started;
        if (pending.method === 'ui' && !pending.sameAsPrevious &&
            location.pathname === pending.previousPath && elapsed >= 4000) {
          display('Google search did not navigate; trying ordinary search link');
          openSearchUrl(pending);
          return;
        }
        if (elapsed < 9000) { display('Loading search: ' + pending.query); schedule(9000 - elapsed); return; }
        // If Google Photos unexpectedly redirects, do not keep navigating forever.
        const result = inspect(pending);
        if (result.status === 'Needs review' && elapsed < 17000) {
          display('Double-checking page for ' + pending.query); schedule(17000 - elapsed); return;
        }
        display(`${pending.query}: ${result.status}`);
        try {
          await message('REPORT', { report: { id: pending.id, status: result.status, detail: result.detail } });
          sessionStorage.removeItem(PENDING_KEY);
          // Fetch only localhost state (never Google) to show the cooldown
          // immediately after the last result, before the next JOB request.
          try {
            const state = await message('STATUS');
            if (state.enabled && state.notChecked > 0 && state.nextAllowedUtc) {
              countdown = { dueAt: Date.parse(state.nextAllowedUtc), setAt: Date.now(),
                eta: state.estimatedSecondsRemaining || 0, remaining: state.remainingSearches || 0 };
              display(progressLabel());
            } else if (!state.enabled && state.notChecked === 0) {
              countdown = null;
              display('Pass complete — review results in desktop app');
            }
          } catch { /* Status failure does not invalidate saved scan result. */ }
        } catch (error) {
          if (elapsed > 5 * 60 * 1000) {
            sessionStorage.removeItem(PENDING_KEY);
            display('Could not report result; scanner may retry: ' + error.message);
          } else {
            display('Report not saved; retrying: ' + error.message);
            schedule(15000); return;
          }
        }
        // Ask immediately whether another search remains. The desktop owns the
        // pacing clock and can report completion without a final 12s wait.
        schedule(350);
        return;
      }
      const next = await message('JOB');
      if (next.state === 'ready' && next.job) {
        countdown = null;
        const job = next.job;
        const previous = PhotoSafeSearchUi.readQuery();
        const sameAsPrevious = previous.found && previous.query &&
          PhotoSafeSearchUi.sameQuery(job.query, previous.query) &&
          location.pathname.startsWith('/search/');
        const pendingJob = { id: job.id, query: job.query, stem: job.stem,
          started: Date.now(), client, method: 'ui',
          previousPath: location.pathname, sameAsPrevious: Boolean(sameAsPrevious) };
        sessionStorage.setItem(PENDING_KEY, JSON.stringify(pendingJob));
        display('Searching exact stem ' + job.query);
        if (sameAsPrevious) {
          display('Using already-open search ' + job.query);
          schedule(9000);
        } else {
          const submitted = PhotoSafeSearchUi.submit(job.query);
          if (submitted.ok) {
            display('Submitted search field ' + job.query);
            schedule(4500);
          } else {
            display('Search box unavailable; trying ordinary search link');
            openSearchUrl(pendingJob);
          }
        }
        return;
      }
      if (next.state === 'complete') { countdown = null; display('Pass complete — review results in desktop app'); }
      else if (next.state === 'daily-limit') { countdown = null; display('Daily limit reached; continue tomorrow'); }
      else if (next.state === 'paused') { countdown = null; display('Desktop scanner paused'); }
      else if (next.state === 'extension-paused') { countdown = null; display('Activate this tab from the extension popup'); }
      else if (next.state === 'waiting' && next.nextAllowedUtc) {
        countdown = { dueAt: Date.parse(next.nextAllowedUtc), setAt: Date.now(),
          eta: next.estimatedSecondsRemaining || 0, remaining: next.remainingSearches || 0 };
        display(progressLabel());
      } else { countdown = null; display(next.message || 'Waiting for next search'); }
      const due = next.nextAllowedUtc ? Date.parse(next.nextAllowedUtc) - Date.now() : 12000;
      schedule(next.state === 'complete' ? 30000 : Math.max(2000, Number.isFinite(due) ? due + 300 : 12000));
    } catch (error) {
      countdown = null;
      display('Check desktop pairing / connection: ' + error.message);
      schedule(15000);
    } finally { busy = false; }
  }

  chrome.runtime.onMessage.addListener((message, _sender, respond) => {
    if (message?.type === 'WAKE') schedule(300);
    if (message?.type === 'DIAGNOSTICS') {
      respond({ ok: true, data: diagnosticsSnapshot() });
    }
  });
  display('Companion loaded; activate tab in extension popup');
  schedule(2500);
}
