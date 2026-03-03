// Va-bank Service Worker — minimal offline shell
// For a hackathon demo, we just do a network-first strategy

self.addEventListener('install', event => {
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(clients.claim());
});

self.addEventListener('fetch', event => {
    // Let the browser handle all fetches normally (network-first)
    // This prevents the service-worker registration error without
    // adding complex caching logic that could interfere with development.
});
