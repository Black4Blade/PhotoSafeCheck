// Read-only visible-page analyzer. A search hit is NOT backup verification.
(function (global) {
  'use strict';
  function inspect({ text, tileCount = 0, pathname = '', expectedQuery = '', actualQuery = '',
                     source = 'unknown', loadSeconds = 0, querySource = 'unavailable', tileStats = {} }) {
    const body = String(text || '').slice(0, 60000);
    const blocked = [
      /unusual traffic/i, /too many requests/i, /verify it.s you/i,
      /this browser or app may not be secure/i, /automated queries/i,
      /account (?:access|temporarily) (?:restricted|blocked)/i,
      /captcha/i, /rate limit/i, /try again later/i
    ];
    if (blocked.some(pattern => pattern.test(body)))
      return { status: 'Paused', detail: 'Google access/security warning detected. Inspect the browser manually.' };
    if (/(?:sign in to|sign in with|log in to) google photos/i.test(body))
      return { status: 'Paused', detail: 'Google Photos requires a normal browser sign-in.' };
    if (expectedQuery && !pathname.startsWith('/search/'))
      return { status: 'Needs review', detail: `Unexpected page path (${pathname}). Expected search ${expectedQuery}.` };

    // Google Photos often rewrites /search/ URLs to opaque IDs (e.g. ChIc…).
    // Only the visible search field (or a still-literal search URL) can indicate
    // whether words and underscores survived. Opaque IDs are NOT text queries.
    if (expectedQuery && !actualQuery)
      return { status: 'Needs review', detail: `Cannot confirm the search text on this Google Photos page (source=${querySource}). Requested ${expectedQuery}. Please inspect the search box manually.` };
    if (expectedQuery && actualQuery && !PhotoSafeSearchUi.sameQuery(expectedQuery, actualQuery))
      return { status: 'Needs review', detail: `Search text differs. Requested ${expectedQuery}; ${querySource} shows ${actualQuery}. Do not count this as a match.` };

    const emptyPhrases = [
      /(?:^|\n)\s*no results\s*(?=\n|$)/i, /no results found/i, /no results for/i, /no photos found/i,
      /nothing found/i, /couldn.t find any results/i,
      /there are no results/i, /try another search/i,
      /no matches found/i, /your search did not match/i,
      /couldn.t find (?:any )?(?:photos|results)/i,
      /לא נמצאו (?:תוצאות|תמונות)/i, /אין תוצאות/i,
      /aucun r[eé]sultat/i, /keine ergebnisse/i, /sin resultados/i,
      /no se encontraron resultados/i, /nenhum resultado/i,
      /нет результатов/i
    ];
    const noResults = emptyPhrases.some(pattern => pattern.test(body));
    if (tileCount > 0 && noResults)
      return { status: 'Needs review', detail: `Contradictory page: ${tileCount} photo-like results alongside a no-results message. Please inspect Google Photos manually.` };
    if (tileCount > 0) {
      const raw = tileStats.rawTiles || tileCount;
      const duplicates = Math.max(0, raw - tileCount);
      const dedupNote = duplicates > 0 ? `; ${duplicates} duplicate display tile(s) ignored` : '';
      return { status: 'Possible match', detail: `${tileCount} distinct photo-like result(s) (${raw} visible tile(s)${dedupNote}; ${source}). This is NOT an original-file verification. Searched ${expectedQuery}.` };
    }
    if (noResults && tileCount === 0)
      return { status: 'Not found by search', detail: `Google Photos displayed an explicit no-results message for ${expectedQuery}. This does NOT prove the file is missing.` };
    // Do not translate an unknown page into a false 'missing' report.
    const signals = `img=${tileStats.images || 0}, bg=${tileStats.backgrounds || 0}, ` +
      `links=${tileStats.photoLinks || 0}, grid=${tileStats.gridCells || 0}`;
    return { status: 'Needs review', detail: `Search result layout unrecognized (tiles=${tileCount}; ${signals}; visibleText=${body.length} chars; waited=${loadSeconds}s). Searched ${expectedQuery}. Use extension > Copy page diagnostics.` };
  }
  global.PhotoSafeAnalyzer = { inspect };
})(typeof globalThis === 'object' ? globalThis : this);
