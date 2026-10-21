// Installation de Finama comme application (Android / ordinateur : bouton ; iPhone : instructions).
let invite = null;
let dotnet = null;

window.addEventListener('beforeinstallprompt', e => {
    e.preventDefault();          // on garde l'invitation pour notre propre bouton
    invite = e;
    if (dotnet) dotnet.invokeMethodAsync('MettreAJour');
});
window.addEventListener('appinstalled', () => {
    invite = null;
    if (dotnet) dotnet.invokeMethodAsync('MettreAJour');
});

export function enregistrer(ref) {
    dotnet = ref;
    if ('serviceWorker' in navigator) navigator.serviceWorker.register('sw-finama.js').catch(() => { });
}

export function etat() {
    const installee = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    const ios = /iphone|ipad|ipod/i.test(navigator.userAgent);
    return { installee, ios, installable: invite !== null };
}

export async function installer() {
    if (!invite) return false;
    invite.prompt();
    const choix = await invite.userChoice;
    invite = null;
    return choix && choix.outcome === 'accepted';
}
