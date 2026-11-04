using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Finama.Web.Layout;

/// <summary>Mise en page principale — session, navigation entre modules, menus et devise.</summary>
public partial class MainLayout
{
    protected override async Task OnInitializedAsync()
    {
        Nav.LocationChanged += SurNavigation;
        DetecterModuleActifDepuisUrl();
        await SecuriserAccesAsync();

        // Redirection automatique vers la grille des modules si on est à la racine
        var uriRelative = Nav.ToBaseRelativePath(Nav.Uri).ToLower();
        if (string.IsNullOrEmpty(uriRelative) || uriRelative == "/")
        {
            Nav.NavigateTo("/modules");
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        // 1. Chargement du module JS et abonnement aux clics extérieurs / touche Échap
        try
        {
            _reference = DotNetObjectReference.Create(this);
            _moduleMenus = await JS.InvokeAsync<IJSObjectReference>("import", "./js/finama-menus.js");
            _abonnementMenus = await _moduleMenus.InvokeAsync<IJSObjectReference>("enregistrerMenus", _reference);
        }
        catch { /* JS indisponible : les menus se referment au prochain clic sur leur bouton */ }

        // 2. Sur téléphone/tablette, la sidebar est un tiroir : elle démarre fermée
        if (await EstEcranEtroitAsync())
        {
            _sidebarOpen = false;
            StateHasChanged();
        }
    }

    [JSInvokable]
    public void FermerMenus()
    {
        if (_detruit || (!_moduleDropdownOpen && !_dropdownOpen)) return;
        _moduleDropdownOpen = false;
        _dropdownOpen = false;
        InvokeAsync(StateHasChanged);
    }

    // Mémorise la dernière page visitée (globalement et par module) pour « Reprendre » et pour le retour dans un module.
    private void SurNavigation(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        // Un changement de page referme toujours les menus ouverts
        if (_moduleDropdownOpen || _dropdownOpen)
        {
            _moduleDropdownOpen = false;
            _dropdownOpen = false;
            InvokeAsync(StateHasChanged);
        }

        _ = MemoriserPageAsync();
    }

    private async Task MemoriserPageAsync()
    {
        if (_detruit) return;
        try
        {
            var relatif = Nav.ToBaseRelativePath(Nav.Uri).Split('?', '#')[0].TrimEnd('/').ToLower();
            if (relatif is "" or "modules" || EstPagePublique(relatif)) return;

            var chemin = "/" + relatif;
            await Stockage.EcrireAsync(ClesStockage.DernierePage, chemin);

            // Pages communes (accueil, notifications…) : jamais retenues comme « dernière page » d'un module
            var module = ModuleDepuisUrl(relatif);
            if (module != "MODULES")
                await Stockage.EcrireAsync(ClesStockage.PageModule(module), chemin);
        }
        catch { /* stockage indisponible : sans conséquence */ }
    }

    public async ValueTask DisposeAsync()
    {
        _detruit = true;
        Nav.LocationChanged -= SurNavigation;

        try
        {
            if (_abonnementMenus is not null)
            {
                await _abonnementMenus.InvokeVoidAsync("dispose");   // retire les écouteurs AVANT de libérer la référence .NET
                await _abonnementMenus.DisposeAsync();
            }
            if (_moduleMenus is not null)
                await _moduleMenus.DisposeAsync();
        }
        catch { /* runtime JS déjà arrêté (rechargement de page) */ }

        _reference?.Dispose();
    }

    protected override async Task OnParametersSetAsync()
    {
        DetecterModuleActifDepuisUrl();

        if (_isInitialized)
        {
            // Même liste que SecuriserAccesAsync : au premier chargement, AuthorizeRouteView affiche
            // « Authorizing » dans ce layout, même pour une page publique (inscription…).
            var uriRelative = Nav.ToBaseRelativePath(Nav.Uri).ToLower();
            if (!EstPagePublique(uriRelative))
            {
                var estConnecte = await AuthApi.EstConnecteAsync();
                if (!estConnecte)
                {
                    Nav.NavigateTo("/connexion", forceLoad: false);
                }
            }
            return;
        }

        await SecuriserAccesAsync();
    }

    private async Task<bool> EstEcranEtroitAsync()
    {
        try
        {
            return _moduleMenus is not null && await _moduleMenus.InvokeAsync<bool>("estEcranEtroit");
        }
        catch { return false; }
    }

    private void DetecterModuleActifDepuisUrl()
    {
        var relatif = Nav.ToBaseRelativePath(Nav.Uri).ToLower();
        _moduleActif = ModuleDepuisUrl(relatif);

        // Factures et clients sont partagés : sans la comptabilité, on reste dans Ventes & Devis
        if (_moduleActif == "COMPTA" && !Modules.Actuels.Comptabilite && Modules.Actuels.GestionCommerciale
            && (relatif.StartsWith("factures") || relatif.StartsWith("referentiel/tiers")))
            _moduleActif = "VENTES";
    }

    private static string ModuleDepuisUrl(string relatif)
    {
        if (relatif.StartsWith("commerce")) return "COMMERCE";
        if (relatif.StartsWith("ventes") || relatif.StartsWith("commercial")) return "VENTES";
        if (relatif.StartsWith("configuration") || relatif.StartsWith("administration") || relatif.StartsWith("utilisateurs") || relatif.StartsWith("entreprise") || relatif.StartsWith("superadmin")) return "ADMIN";
        if (relatif.StartsWith("modules") || relatif.StartsWith("notifications")) return "MODULES";   // pages communes à tous
        return "COMPTA";
    }

    private void ToggleModuleDropdown()
    {
        _moduleDropdownOpen = !_moduleDropdownOpen;
        _dropdownOpen = false;
    }

    private void AllerAuxModules()
    {
        _moduleDropdownOpen = false;
        Nav.NavigateTo("/modules");
    }

    private async Task SelectionnerModuleAsync(string module)
    {
        _moduleActif = module;
        _moduleDropdownOpen = false;

        var defaut = module switch
        {
            "COMMERCE" => "/commerce/produits",
            "ADMIN" => "/entreprise",
            "VENTES" => "/ventes",
            _ => "/"
        };

        string? derniere = null;
        try { derniere = await Stockage.LireAsync(ClesStockage.PageModule(module)); }
        catch { /* stockage indisponible */ }

        // Une valeur mémorisée d'un autre module (ou d'une ancienne version) est ignorée
        var valide = !string.IsNullOrWhiteSpace(derniere) && derniere.StartsWith('/')
                     && ModuleDepuisUrl(derniere.TrimStart('/').ToLower()) == module;
        Nav.NavigateTo(valide ? derniere! : defaut);
    }

    private async Task SecuriserAccesAsync()
    {
        var uriRelative = Nav.ToBaseRelativePath(Nav.Uri).ToLower();

        if (EstPagePublique(uriRelative))
        {
            _verificationEnCours = false;
            _isInitialized = true;
            return;
        }

        var etat = await EtatSessionAsync();

        if (etat != "valide")
        {
            _verificationEnCours = false;
            _isInitialized = true;
            Nav.NavigateTo(etat == "expiree" ? "/connexion?expire=1" : "/connexion");
        }
        else
        {
            if (string.IsNullOrEmpty(_userNom))
            {
                await ChargerDonneesSessionAsync();
            }
            await Modules.ChargerAsync();   // menus selon l'abonnement de l'entreprise
            _verificationEnCours = false;
            _isInitialized = true;
            StateHasChanged();
        }
    }

    private async Task ChargerDonneesSessionAsync()
    {
        _userNom = await AuthApi.GetUserNomAsync() ?? "";
        _entrepriseNom = await AuthApi.GetEntrepriseNomAsync() ?? "";

        var baseDevise = await AuthApi.GetDeviseSymboleAsync() ?? "XOF";
        _listeDevises = await EntrepriseApi.GetDevisesActivesAsync();

        if (_listeDevises != null && _listeDevises.Any())
        {
            var deviseAssociee = _listeDevises.FirstOrDefault(d => d.Code == baseDevise || d.Symbole == baseDevise);
            if (deviseAssociee != null)
            {
                _deviseActiveCode = deviseAssociee.Code;
                _deviseSymbole = deviseAssociee.Symbole;
            }
            else
            {
                _deviseActiveCode = baseDevise;
                _deviseSymbole = baseDevise;
            }
        }

        if (!string.IsNullOrEmpty(_userNom))
        {
            var parts = _userNom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _userInitiales = parts.Length >= 2
                ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
                : _userNom[..1].ToUpper();
        }
    }

    /// <summary>
    /// "absente" : jamais connecté ; "expiree" : jeton expiré ET pas de renouvellement possible ;
    /// "valide" : jeton valide, ou expiré mais renouvelable (AuthHeaderHandler le renouvelle au premier appel).
    /// Lecture locale uniquement : aucun appel réseau, donc aucun écran de chargement en plus.
    /// </summary>
    private async Task<string> EtatSessionAsync()
    {
        var token = await AuthApi.GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return "absente";
        if (!JwtOutils.EstExpire(token)) return "valide";

        string? refresh = null;
        refresh = await Stockage.LireAsync(ClesStockage.Refresh);
        return string.IsNullOrEmpty(refresh) ? "expiree" : "valide";
    }

    /// <summary>Pages accessibles sans être connecté (jamais redirigées vers la connexion).</summary>
    private static bool EstPagePublique(string relatif) =>
        relatif.StartsWith("connexion") || relatif.StartsWith("login") || relatif.StartsWith("register")
        || relatif.StartsWith("inscription") || relatif.StartsWith("mot-de-passe-oublie");

    private void ToggleSidebar()
    {
        _sidebarOpen = !_sidebarOpen;
    }

    // Sur petit écran, on referme le tiroir après le choix d'une page.
    private async Task GererClicNav()
    {
        if (_sidebarOpen && await EstEcranEtroitAsync())
            _sidebarOpen = false;
    }

    private async Task Deconnecter()
    {
        _moduleDropdownOpen = false;
        _dropdownOpen = false;
        await AuthApi.LogoutAsync();
        Nav.NavigateTo("/connexion", forceLoad: true);   // repart de zéro : aucun droit ni menu du compte précédent
    }

    private void ToggleDropdown()
    {
        _dropdownOpen = !_dropdownOpen;
        _moduleDropdownOpen = false;
    }

    private void ClicDevise(string code, string symbole)
    {
        _deviseActiveCode = code;
        _deviseSymbole = symbole;
        _dropdownOpen = false;
    }
}
