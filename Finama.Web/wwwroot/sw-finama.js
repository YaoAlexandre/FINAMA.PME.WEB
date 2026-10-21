// Service worker minimal : rend Finama installable, SANS mise en cache.
// Volontaire : une application de gestion doit toujours afficher les données et la version à jour
// (pas de risque d'ancienne version restée en cache après une mise en production).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { /* passage direct au réseau */ });
