// Partage d'un PDF par le menu natif du téléphone (WhatsApp, e-mail, Bluetooth…).
// Renvoie "partage", "annule" ou "non-supporte" (ordinateur ou navigateur sans partage de fichiers).
export async function partagerPdf(base64, nomFichier, texte) {
    try {
        const octets = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const fichier = new File([octets], nomFichier, { type: 'application/pdf' });
        if (!navigator.canShare || !navigator.canShare({ files: [fichier] })) return 'non-supporte';
        await navigator.share({ files: [fichier], text: texte, title: nomFichier });
        return 'partage';
    } catch (e) {
        return e && e.name === 'AbortError' ? 'annule' : 'non-supporte';
    }
}

export function ouvrir(url) {
    window.open(url, '_blank', 'noopener');
}
