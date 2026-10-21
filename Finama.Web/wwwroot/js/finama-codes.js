// Codes-barres et QR codes : scan par caméra (html5-qrcode) et génération de QR (qrcode-generator).
// Les bibliothèques sont chargées à la demande : aucun poids ajouté au démarrage de l'application.

const scripts = {};

function chargerScript(src) {
    return scripts[src] ??= new Promise((ok, ko) => {
        const s = document.createElement('script');
        s.src = src;
        s.onload = ok;
        s.onerror = () => { delete scripts[src]; ko(new Error('Chargement impossible : ' + src)); };
        document.head.appendChild(s);
    });
}

// ─── Scan par caméra ─────────────────────────────────────────────────────────
let scanner = null;

/**
 * Démarre la caméra arrière dans l'élément #elementId.
 * Chaque code lu appelle dotnetRef.SurCodeLu(texte). En mode continu, la caméra reste ouverte (caisse, inventaire).
 */
export async function demarrerScan(elementId, dotnetRef, continu) {
    if (!window.isSecureContext) throw new Error('HTTPS_REQUIS');
    await chargerScript('lib/html5-qrcode/html5-qrcode.min.js');
    await arreterScan();

    const lib = window.__Html5QrcodeLibrary__;
    const F = lib.Html5QrcodeSupportedFormats;
    scanner = new lib.Html5Qrcode(elementId, {
        verbose: false,
        useBarCodeDetectorIfSupported: true,   // détecteur natif du téléphone quand il existe (plus rapide)
        formatsToSupport: [F.QR_CODE, F.EAN_13, F.EAN_8, F.UPC_A, F.UPC_E, F.CODE_128, F.CODE_39, F.ITF]
    });

    let dernier = '', instant = 0;
    try {
        await scanner.start(
            { facingMode: 'environment' },
            {
                fps: 12,
                qrbox: (l, h) => ({ width: Math.floor(l * 0.85), height: Math.floor(Math.min(h * 0.6, l * 0.55)) })
            },
            async (texte) => {
                const maintenant = Date.now();
                if (texte === dernier && maintenant - instant < 1800) return;   // même article tenu devant la caméra
                dernier = texte;
                instant = maintenant;
                bip();
                if (navigator.vibrate) navigator.vibrate(60);
                try { await dotnetRef.invokeMethodAsync('SurCodeLu', texte); } catch { /* composant fermé */ }
                if (!continu) await arreterScan();
            },
            () => { /* aucune lecture sur cette image : normal */ });
    } catch (e) {
        scanner = null;
        const nom = (e && (e.name || e.toString())) || '';
        if (nom.includes('NotAllowed') || nom.includes('Permission')) throw new Error('CAMERA_REFUSEE');
        if (nom.includes('NotFound') || nom.includes('Overconstrained')) throw new Error('CAMERA_ABSENTE');
        throw new Error('CAMERA_ERREUR');
    }
}

export async function arreterScan() {
    if (!scanner) return;
    const s = scanner;
    scanner = null;
    try {
        if (s.isScanning) await s.stop();
        s.clear();
    } catch { /* déjà arrêté */ }
}

function bip() {
    try {
        const ctx = new (window.AudioContext || window.webkitAudioContext)();
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.frequency.value = 1250;
        gain.gain.value = 0.08;
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start();
        osc.stop(ctx.currentTime + 0.09);
        osc.onended = () => ctx.close();
    } catch { /* son indisponible */ }
}

// ─── Génération de QR codes ──────────────────────────────────────────────────

/** Renvoie le QR code de `texte` en SVG redimensionnable (accents gérés). */
export async function qrSvg(texte) {
    await chargerScript('lib/qrcode-generator/qrcode.js');
    const qrcode = window.qrcode;
    qrcode.stringToBytes = qrcode.stringToBytesFuncs['UTF-8'];
    const qr = qrcode(0, 'M');
    qr.addData(texte);
    qr.make();
    return qr.createSvgTag({ cellSize: 4, margin: 0, scalable: true });
}

/** Plusieurs QR d'un coup (planche d'étiquettes). */
export async function qrSvgListe(textes) {
    const res = [];
    for (const t of textes) res.push(await qrSvg(t));
    return res;
}

export function imprimer() {
    requestAnimationFrame(() => requestAnimationFrame(() => window.print()));
}
