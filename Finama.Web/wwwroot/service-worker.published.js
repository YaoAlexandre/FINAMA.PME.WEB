// Production : l'application (fichiers .wasm, .dll, .js, .css, images) est gardée sur l'appareil.
//  • démarrage instantané, même avec un réseau lent ;
//  • ouverture possible sans réseau (les données, elles, viennent toujours de l'API) ;
//  • chaque publication produit une nouvelle liste de fichiers (service-worker-assets.js) : l'ancienne version
//    est remplacée dès que l'utilisateur accepte la mise à jour (bandeau « Nouvelle version disponible »).
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(installer()));
self.addEventListener('activate', event => event.waitUntil(activer()));
self.addEventListener('fetch', event => event.respondWith(servir(event)));
self.addEventListener('message', event => { if (event.data === 'FINAMA_MAJ') self.skipWaiting(); });

const prefixe = 'finama-app-';
const nomCache = `${prefixe}${self.assetsManifest.version}`;
const inclus = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.svg$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const exclus = [/^service-worker\.js$/];
const racine = new URL('/', self.origin);
const urlsApplication = self.assetsManifest.assets.map(a => new URL(a.url, racine).href);

async function installer() {
    // Chaque fichier est mis en cache séparément : un seul fichier en échec (modifié après la publication,
    // absent du serveur…) ne doit pas empêcher l'installation — il sera simplement chargé depuis le réseau.
    const requetes = self.assetsManifest.assets
        .filter(a => inclus.some(m => m.test(a.url)))
        .filter(a => !exclus.some(m => m.test(a.url)))
        .map(a => new Request(a.url, { integrity: a.hash, cache: 'no-cache' }));
    const cache = await caches.open(nomCache);
    const resultats = await Promise.allSettled(requetes.map(async r => {
        const reponse = await fetch(r);
        if (!reponse.ok) throw new Error(`${r.url} : ${reponse.status}`);
        await cache.put(r, reponse);
    }));
    const echecs = resultats.filter(x => x.status === 'rejected').map(x => x.reason && x.reason.message);
    if (echecs.length) console.warn(`[Finama] ${echecs.length} fichier(s) non mis en cache (chargés depuis le réseau) :`, echecs);
}

async function activer() {
    const noms = await caches.keys();
    await Promise.all(noms.filter(n => n.startsWith(prefixe) && n !== nomCache).map(n => caches.delete(n)));
    await self.clients.claim();
}

async function servir(event) {
    // Seuls les fichiers de l'application sont servis depuis le cache : l'API (autre domaine) passe toujours par le réseau.
    if (event.request.method === 'GET') {
        const navigation = event.request.mode === 'navigate' && !urlsApplication.includes(event.request.url);
        const cache = await caches.open(nomCache);
        const enCache = await cache.match(navigation ? 'index.html' : event.request);
        if (enCache) return enCache;
    }
    return fetch(event.request);
}

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