// service-worker.js
const CACHE = 'test-wasm-v1';   // bump on every release

// Precache a minimal set. Everything else (incl. _framework/*) is cached on first request.
// Note: cache.addAll fails entirely if one file is missing (404), so list only existing files.
const PRECACHE = ['./', './index.html', './timer.js', './webrtc-interop.js', './manifest.webmanifest', './favicon.ico'];

self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE)
            .then((cache) => cache.addAll(PRECACHE))
            .then(() => self.skipWaiting())
    );
});

self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            // Delete caches of previous versions
            .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    const url = new URL(req.url);

    // Handle only same-origin GET requests.
    // Signaling (wss://) and WebRTC traffic do not go through fetch anyway.
    if (req.method !== 'GET' || url.origin !== self.location.origin) return;

    event.respondWith(
        caches.match(req).then((cached) => {
            // Cache-first: serve from cache if available
            if (cached) return cached;

            return fetch(req).then((res) => {
                // Cache only complete, successful responses (no 206 partial responses)
                if (res.ok && res.status === 200) {
                    const copy = res.clone();
                    caches.open(CACHE).then((cache) => cache.put(req, copy));
                }
                return res;
            });
        })
    );
});