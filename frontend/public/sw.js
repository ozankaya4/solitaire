/* Solitaire service worker — offline-first app shell caching.
 *
 * The engine and the graded-deal library are bundled into the hashed JS, so
 * runtime-caching same-origin GETs covers them. Guests play fully offline after
 * the first load; the app never contacts a backend. Bump CACHE to invalidate. */

const CACHE = 'solitaire-cache-v2'; // v2: /api/* is never intercepted or cached
const PRECACHE = [
  '/',
  '/index.html',
  '/manifest.webmanifest',
  '/vite.svg',
  '/icons/icon-192.png',
  '/icons/icon-512.png',
  '/icons/icon-512-maskable.png',
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      .then((cache) => cache.addAll(PRECACHE))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key))),
      )
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') {
    return;
  }
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) {
    return; // never touch cross-origin
  }
  if (url.pathname.startsWith('/api/')) {
    // Never intercept API calls: they are dynamic and authenticated. Caching them
    // would freeze the leaderboard/session/sync state at first sight, and the
    // /api/* proxy only exists on the network, not in any cache.
    return;
  }

  // SPA navigations: network-first, fall back to the cached shell offline.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((response) => {
          const copy = response.clone();
          void caches.open(CACHE).then((cache) => cache.put('/index.html', copy));
          return response;
        })
        .catch(() => caches.match('/index.html').then((cached) => cached || caches.match('/'))),
    );
    return;
  }

  // Static assets (hashed JS/CSS/fonts, icons): cache-first, then populate.
  event.respondWith(
    caches.match(request).then(
      (cached) =>
        cached ||
        fetch(request).then((response) => {
          if (response.ok) {
            const copy = response.clone();
            void caches.open(CACHE).then((cache) => cache.put(request, copy));
          }
          return response;
        }),
    ),
  );
});
