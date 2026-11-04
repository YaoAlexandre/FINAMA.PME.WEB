// Logo du ticket de caisse : niveaux de gris sur fond blanc, quel que soit le navigateur ou le pilote d'imprimante.
export function noirEtBlanc(dataUrl) {
    return new Promise(ok => {
        const img = new Image();
        img.onload = () => {
            const c = document.createElement('canvas');
            c.width = img.naturalWidth; c.height = img.naturalHeight;
            const g = c.getContext('2d');
            g.fillStyle = '#fff'; g.fillRect(0, 0, c.width, c.height);   // transparence → blanc
            g.drawImage(img, 0, 0);
            const d = g.getImageData(0, 0, c.width, c.height), p = d.data;
            for (let i = 0; i < p.length; i += 4) {
                const y = 0.299 * p[i] + 0.587 * p[i + 1] + 0.114 * p[i + 2];
                p[i] = p[i + 1] = p[i + 2] = y;
            }
            g.putImageData(d, 0, 0);
            ok(c.toDataURL('image/png'));
        };
        img.onerror = () => ok(dataUrl);
        img.src = dataUrl;
    });
}