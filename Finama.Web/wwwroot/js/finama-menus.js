// Menus déroulants de MainLayout : fermeture au clic extérieur et à la touche Échap.
// Un abonnement par instance de layout : dispose() retire exactement ses écouteurs.
export function enregistrerMenus(dotnetRef) {
    let actif = true;

    const menuOuvert = () => document.querySelector('.module-menu.show, .menu-zone .dropdown-menu.show') !== null;

    const fermer = () => {
        if (!actif || !menuOuvert()) return;
        // Le layout peut avoir été détruit entre-temps (déconnexion, redirection) : on ignore l'échec.
        dotnetRef.invokeMethodAsync('FermerMenus').catch(() => { });
    };

    const surClic = (e) => {
        if (e.target instanceof Element && e.target.closest('.menu-zone')) return;
        fermer();
    };

    const surTouche = (e) => {
        if (e.key === 'Escape') fermer();
    };

    document.addEventListener('click', surClic);
    document.addEventListener('keydown', surTouche);

    return {
        dispose: () => {
            actif = false;
            document.removeEventListener('click', surClic);
            document.removeEventListener('keydown', surTouche);
        }
    };
}

// Même seuil que le CSS (tiroir sous 992 px).
export function estEcranEtroit() {
    return window.matchMedia('(max-width: 991.98px)').matches;
}