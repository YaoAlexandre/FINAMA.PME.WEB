// Caisse hors connexion : petite base locale (IndexedDB) sur l'appareil.
//  • « contexte » : magasin, session de caisse et catalogue, mis à jour à chaque chargement en ligne ;
//  • « file »     : ventes faites sans réseau, en attente d'envoi au serveur ;
//  • « compteurs »: numérotation des tickets provisoires (HL-CAISSE-AAMMJJ-n) par caisse et par jour.
const NOM_BASE = 'finama-caisse';
let base = null;

function ouvrir() {
    if (base) return Promise.resolve(base);
    return new Promise((ok, ko) => {
        const req = indexedDB.open(NOM_BASE, 1);
        req.onupgradeneeded = () => {
            const db = req.result;
            if (!db.objectStoreNames.contains('cle')) db.createObjectStore('cle');
            if (!db.objectStoreNames.contains('file')) db.createObjectStore('file', { keyPath: 'id' });
        };
        req.onsuccess = () => { base = req.result; ok(base); };
        req.onerror = () => ko(req.error);
    });
}

async function transaction(magasin, mode, action) {
    const db = await ouvrir();
    return new Promise((ok, ko) => {
        const tx = db.transaction(magasin, mode);
        const resultat = action(tx.objectStore(magasin));
        tx.oncomplete = () => ok(resultat?.result);
        tx.onerror = () => ko(tx.error);
    });
}

export function enLigne() { return navigator.onLine; }

export function lire(cle) { return transaction('cle', 'readonly', s => s.get(cle)).then(v => v ?? null); }
export function ecrire(cle, valeur) { return transaction('cle', 'readwrite', s => s.put(valeur, cle)); }

/** Prochain numéro de ticket provisoire pour cette caisse et ce jour (jamais réutilisé, même après rechargement). */
export async function prochainNumero(cleCompteur) {
    const db = await ouvrir();
    return new Promise((ok, ko) => {
        const tx = db.transaction('cle', 'readwrite');
        const store = tx.objectStore('cle');
        const req = store.get(cleCompteur);
        let suivant = 1;
        req.onsuccess = () => { suivant = (req.result || 0) + 1; store.put(suivant, cleCompteur); };
        tx.oncomplete = () => ok(suivant);
        tx.onerror = () => ko(tx.error);
    });
}

export function fileAjouter(vente) { return transaction('file', 'readwrite', s => s.put(vente)); }
export function fileRetirer(id) { return transaction('file', 'readwrite', s => s.delete(id)); }
export async function fileLister() {
    const liste = await transaction('file', 'readonly', s => s.getAll());
    return (liste || []).sort((a, b) => (a.dateVenteLocale || '').localeCompare(b.dateVenteLocale || ''));
}
export async function fileMarquerErreur(id, message) {
    const db = await ouvrir();
    return new Promise((ok, ko) => {
        const tx = db.transaction('file', 'readwrite');
        const store = tx.objectStore('file');
        const req = store.get(id);
        req.onsuccess = () => { const v = req.result; if (v) { v.statut = 'erreur'; v.erreur = message; store.put(v); } };
        tx.oncomplete = () => ok();
        tx.onerror = () => ko(tx.error);
    });
}
export async function fileRemettreEnAttente() {
    const liste = await fileLister();
    for (const v of liste.filter(x => x.statut === 'erreur')) { v.statut = 'attente'; v.erreur = null; await fileAjouter(v); }
}
