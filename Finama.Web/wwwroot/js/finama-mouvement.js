// Préférences d'animation du système (« Réduire les animations »).
export function reductionDemandee() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}
