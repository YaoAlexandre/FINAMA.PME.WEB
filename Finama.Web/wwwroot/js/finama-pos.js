// Utilitaires de la caisse (POS) : impression du ticket, focus du scanner, partage WhatsApp.

// Imprime uniquement le ticket (#ticket-impression) via la feuille de style @media print de Caisse.razor.
export function imprimerTicket() {
    // Laisse le navigateur peindre le ticket avant d'ouvrir la boîte d'impression
    requestAnimationFrame(() => requestAnimationFrame(() => window.print()));
}

export function focusScanner() {
    document.getElementById('pos-scanner-input')?.focus();
}

export function ouvrirLien(url) {
    window.open(url, '_blank', 'noopener');
}
