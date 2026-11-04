// Développement (dotnet run) : aucun cache, pour voir chaque modification immédiatement.
// En publication, ce fichier est remplacé par service-worker.published.js (voir Finama.Web.csproj).
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', e => e.waitUntil(self.clients.claim()));
self.addEventListener('fetch', () => { });

// ── Notifications (push) ──
self.addEventListener("push", event => {
    let d = {};
    try { d = event.data ? event.data.json() : {}; } catch { d = { corps: event.data ? event.data.text() : "" }; }
    event.waitUntil(self.registration.showNotification(d.titre || "Finama", {
        body: d.corps || "",
        icon: "icons/icon-192.png",
        badge: "icons/icon-maskable-192.png",
        tag: d.tag || undefined,
        renotify: !!d.tag,
        data: { url: d.url || "/modules" }
    }));
});

self.addEventListener("notificationclick", event => {
    event.notification.close();
    const cible = new URL(event.notification.data?.url || "/modules", self.location.origin).href;
    event.waitUntil((async () => {
        const fenetres = await self.clients.matchAll({ type: "window", includeUncontrolled: true });
        for (const f of fenetres) {
            if (new URL(f.url).origin === self.location.origin) { await f.focus(); return f.navigate(cible); }
        }
        return self.clients.openWindow(cible);
    })());
});
