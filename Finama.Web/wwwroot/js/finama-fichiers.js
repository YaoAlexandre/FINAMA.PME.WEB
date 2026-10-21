// Téléchargement d'un fichier reçu de l'API (octets en base64) sans ouvrir de nouvel onglet.
export function telecharger(nomFichier, typeMime, base64) {
    const octets = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const url = URL.createObjectURL(new Blob([octets], { type: typeMime }));
    const lien = document.createElement('a');
    lien.href = url;
    lien.download = nomFichier;
    document.body.appendChild(lien);
    lien.click();
    lien.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
