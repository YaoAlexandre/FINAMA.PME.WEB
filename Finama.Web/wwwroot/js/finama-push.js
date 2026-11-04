// Abonnement de cet appareil aux notifications Finama.
function base64VersOctets(b64) {
    const pad = '='.repeat((4 - b64.length % 4) % 4);
    const brut = atob((b64 + pad).replace(/-/g, '+').replace(/_/g, '/'));
    return Uint8Array.from([...brut].map(c => c.charCodeAt(0)));
}

function versBase64Url(buffer) {
    return btoa(String.fromCharCode(...new Uint8Array(buffer))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function nomAppareil() {
    const ua = navigator.userAgent;
    const sys = /iPhone|iPad/.test(ua) ? 'iPhone' : /Android/.test(ua) ? 'Android' : /Windows/.test(ua) ? 'Windows' : /Mac/.test(ua) ? 'Mac' : 'Appareil';
    const nav = /Edg\//.test(ua) ? 'Edge' : /Chrome\//.test(ua) ? 'Chrome' : /Safari\//.test(ua) ? 'Safari' : /Firefox\//.test(ua) ? 'Firefox' : 'Navigateur';
    return `${sys} · ${nav}`;
}

/** Service worker actif, installé si nécessaire. Ne bloque jamais plus de 10 secondes. */
async function enregistrement() {
    if (!('serviceWorker' in navigator)) return null;
    let reg = await navigator.serviceWorker.getRegistration();
    if (!reg) reg = await navigator.serviceWorker.register('service-worker.js');
    if (reg.active) return reg;
    const pret = navigator.serviceWorker.ready;
    const delai = new Promise(r => setTimeout(() => r(null), 10000));
    return await Promise.race([pret, delai]);
}

export function etat() {
    const supporte = 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    const ios = /iphone|ipad|ipod/i.test(navigator.userAgent);
    const installee = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    return { supporte, ios, installee, permission: supporte ? Notification.permission : 'non-supporte' };
}

/** Adresse de l'abonnement de cet appareil, ou null (sans jamais attendre l'installation du service worker). */
export async function abonnementActuel() {
    if (!('serviceWorker' in navigator)) return null;
    const reg = await navigator.serviceWorker.getRegistration();
    if (!reg) return null;
    const sub = await reg.pushManager.getSubscription();
    return sub ? sub.endpoint : null;
}

// Doit être appelé directement depuis un clic (exigence des navigateurs)
export async function abonner(clePublique) {
    const permission = await Notification.requestPermission();
    if (permission !== 'granted') return { ok: false, raison: permission };
    const reg = await enregistrement();
    if (!reg) return { ok: false, raison: 'service-worker' };
    let sub = await reg.pushManager.getSubscription();
    if (!sub) sub = await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: base64VersOctets(clePublique) });
    return {
        ok: true,
        endpoint: sub.endpoint,
        p256dh: versBase64Url(sub.getKey('p256dh')),
        auth: versBase64Url(sub.getKey('auth')),
        appareil: nomAppareil()
    };
}

export async function desabonner() {
    if (!('serviceWorker' in navigator)) return null;
    const reg = await navigator.serviceWorker.getRegistration();
    const sub = reg ? await reg.pushManager.getSubscription() : null;
    if (!sub) return null;
    const endpoint = sub.endpoint;
    await sub.unsubscribe();
    return endpoint;
}