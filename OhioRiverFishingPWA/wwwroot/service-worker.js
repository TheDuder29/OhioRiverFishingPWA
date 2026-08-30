const CACHE_NAME = 'ohio-river-fishing-cache-v4';
const urlsToCache = [
    '/',
    '/css/app.css',
    '/manifest.json',
    '/favicon.svg?v=4'
];

self.addEventListener('install', (event) => {
    // Take over immediately without waiting for old SW to finish
    self.skipWaiting();
    event.waitUntil(
        caches.open(CACHE_NAME).then((cache) => cache.addAll(urlsToCache))
    );
});

self.addEventListener('activate', (event) => {
    // Claim all clients immediately and delete old caches
    event.waitUntil(
        Promise.all([
            self.clients.claim(),
            caches.keys().then((keys) =>
                Promise.all(
                    keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))
                )
            )
        ])
    );
});

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // Never intercept Blazor framework files, MudBlazor, or browser internals
    if (url.pathname.startsWith('/_framework/') ||
        url.pathname.startsWith('/_content/') ||
        url.pathname === '/favicon.ico') {
        return; // Pass straight through to network
    }

    event.respondWith(
        caches.match(event.request).then((response) => {
            return response || fetch(event.request);
        })
    );
});
