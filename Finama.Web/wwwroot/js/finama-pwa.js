// Application installable + mise à jour + état du réseau.
let invite = null;
let dotnet = null;

window.addEventListener('beforeinstallprompt', e => {
    e.preventDefault();
    invite = e;
    if (dotnet) dotnet.invokeMethodAsync('MettreAJour');
});
window.addEventListener('appinstalled', () => {
    invite = null;
    if (dotnet) dotnet.invokeMethodAsync('MettreAJour');
});

export function enregistrer(ref) {
    dotnet = ref;
    window.addEventListener('online', () => dotnet.invokeMethodAsync('ReseauChange', true));
    window.addEventListener('offline', () => dotnet.invokeMethodAsync('ReseauChange', false));

    if (!('serviceWorker' in navigator)) return;

    // Rechargement automatique quand la nouvelle version prend la main (jamais à la toute première visite)
    const avaitVersion = !!navigator.serviceWorker.controller;
    let recharge = false;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (!avaitVersion || recharge) return;
        recharge = true;
        location.reload();
    });

    navigator.serviceWorker.register('service-worker.js').then(reg => {
        const signaler = () => {
            if (reg.waiting && navigator.serviceWorker.controller) dotnet.invokeMethodAsync('MiseAJourDisponible');
        };
        signaler();
        reg.addEventListener('updatefound', () => {
            const nouveau = reg.installing;
            if (nouveau) nouveau.addEventListener('statechange', () => { if (nouveau.state === 'installed') signaler(); });
        });
        // Vérifie régulièrement s'il y a une nouvelle version (application laissée ouverte toute la journée en caisse)
        setInterval(() => reg.update().catch(() => { }), 30 * 60 * 1000);
    }).catch(() => { });
}

export async function appliquerMiseAJour() {
    const reg = await navigator.serviceWorker?.getRegistration();
    if (reg?.waiting) reg.waiting.postMessage('FINAMA_MAJ');
    else location.reload();
}

export function etat() {
    const installee = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    const ios = /iphone|ipad|ipod/i.test(navigator.userAgent);
    return { installee, ios, installable: invite !== null, enLigne: navigator.onLine };
}

export async function installer() {
    if (!invite) return false;
    invite.prompt();
    const choix = await invite.userChoice;
    invite = null;
    return choix && choix.outcome === 'accepted';
}
