using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Finama.Web.Shared;
using Finama.Web.Features.Commerce.Components;

namespace Finama.Web.Features.Commerce.Pages;

/// <summary>Caisse — état de la page, cycle de vie, catalogue et scanner, raccourcis clavier.</summary>
public partial class Caisse
{
    private ConfirmDialog _confirm = default!;
    private ScannerCamera _scanner = default!;
    private IJSObjectReference? _jsCodes;
    private string? _qrTicket;
    private IJSObjectReference? _jsPos;

    // ── Contexte ──
    private Guid? _magasinId;
    private string _magasinNom = "";
    private string? _magasinAdresse;
    private string? _magasinTelephone;
    private string _entrepriseNom = "";
    private string? _logo;
    private string _caisseLibelle = "";
    private List<CaissePosDto> _caisses = new();
    private SessionPosDto? _session;
    private Guid _caisseChoisieId;
    private decimal _fondDeCaisse;
    private bool _chargementSession = true;
    private bool _enCoursAction;

    // ── Catalogue / panier ──
    private bool _chargementArticles;
    private string _rechercheArticle = "";
    private List<ProduitCommerceDto> _articles = new();
    private List<ProduitCommerceDto> _articlesFiltres = new();
    private List<LignePanierItem> _panier = new();
    private decimal TotalPanier => _panier.Sum(l => l.Total);

    // ── Client ──
    private ClientCommerceDto? _client;
    private string _rechercheClient = "";
    private List<ClientCommerceDto> _resultatsClients = new();
    private bool _afficherResultatsClient;

    // ── Paiement ──
    private bool _afficherPaiement;
    private bool _enCoursEncaissement;
    private Guid _clientRequestId = Guid.NewGuid();
    private List<PaiementSaisi> _paiements = new();

    // ── Ticket ──
    private RecuCaisse? _dernierRecu;      // dernière vente (bandeau + réimpression)
    private RecuCaisse? _recuAImprimer;    // ticket rendu dans la zone d'impression
    private bool _impressionAuto = true;

    private decimal TotalPaye => _paiements.Sum(p => p.Montant);
    private decimal Reste => Math.Max(0, TotalPanier - TotalPaye);
    private decimal Rendu => Math.Max(0, TotalPaye - TotalPanier);
    private decimal Especes => _paiements.Where(p => p.Mode == ModePaiementPos.Especes).Sum(p => p.Montant);
    private bool RenduPossible => Rendu <= Especes;
    private bool PaiementValide => _paiements.Any() && TotalPanier > 0 && Reste == 0 && RenduPossible && _paiements.All(p => p.Montant > 0);

    // ── Clôture ──
    private bool _afficherCloture;
    private decimal _declEspeces, _declCarte, _declMobile;
    private string? _commentaireCloture;
    private bool _afficherImages = true;
    // Zone image sur toutes les tuiles seulement si au moins un article affiché a une photo (sinon : tuiles compactes comme avant)
    private bool AvecPhotos => _articlesFiltres.Any(a => Images.Url(a.Id) is not null);

    private static string Initiales(string nom)
    {
        var mots = nom.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(m => m.Length > 2 || char.IsDigit(m[0])).ToList();
        if (mots.Count == 0) mots = nom.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        return string.Concat(mots.Take(2).Select(m => char.ToUpperInvariant(m[0])));
    }

    protected override async Task OnInitializedAsync()
    {
                try { _afficherImages = await Stockage.LireAsync(ClesStockage.CaisseImages) != "0"; } catch { }
        _ = Images.ChargerAsync().ContinueWith(_ => InvokeAsync(StateHasChanged));
        _chargementSession = true;
        try { _jsHorsLigne = await JS.InvokeAsync<IJSObjectReference>("import", "./js/finama-horsligne.js"); } catch { }

        var enLigne = await EnLigneAsync();
        if (enLigne)
        {
            await ResoudreMagasinAsync();
            if (_magasinId.HasValue) await ChargerSessionAsync();
        }
        if (!enLigne || (_magasinId is null && EstErreurReseau()))
            await ChargerContexteLocalAsync();

        _chargementSession = false;
        await RafraichirFileAsync();
        _ = BoucleSynchronisationAsync();
    }
            private async Task BasculerImages()
    {
        _afficherImages = !_afficherImages;
        await Stockage.EcrireAsync(ClesStockage.CaisseImages, _afficherImages ? "1" : "0");
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            _jsPos = await JS.InvokeAsync<IJSObjectReference>("import", "./js/finama-pos.js");
            _entrepriseNom = await Stockage.LireAsync(ClesStockage.Entreprise) ?? "";
            _impressionAuto = await Stockage.LireAsync(ClesStockage.ImpressionAuto) != "0";
            _logo = await Stockage.LireAsync(ClesStockage.Logo);   // cache : ticket imprimable même hors réseau
            StateHasChanged();
        }
        catch { /* JS indisponible : impression désactivée */ }

        // Logo à jour depuis l'API (remplace le cache)
        var logo = await Api.GetAsync<LogoReponse>("api/entreprise/logo");
        if (logo.Succes)   // sinon on garde le cache
        {
            _logo = logo.Donnees?.DataUrl;
            await Stockage.EcrireAsync(ClesStockage.Logo, _logo);   // null = suppression
        }
        // Ticket de caisse : logo en noir et blanc (aperçu, impression et réimpression identiques)
        if (!string.IsNullOrEmpty(_logo))
        {
            try
            {
                await using var jsImage = await JS.InvokeAsync<IJSObjectReference>("import", ModulesJs.Image);
                _logo = await jsImage.InvokeAsync<string>("noirEtBlanc", _logo);
            }
            catch { /* logo d'origine */ }
        }
    }

    private sealed record LogoReponse(string? DataUrl);

    private async Task ResoudreMagasinAsync()
    {
        var magasins = await MagasinsApi.GetMagasinsAsync();
        var pdv = magasins.Where(m => m.EstActif && m.Type == TypeMagasin.PointDeVente).ToList();
        var actifId = await MagasinsApi.GetMagasinActifIdAsync();

        var choisi = pdv.FirstOrDefault(m => m.Id == actifId) ?? pdv.FirstOrDefault(m => m.EstPrincipal) ?? pdv.FirstOrDefault();
        _magasinId = choisi?.Id;
        _magasinNom = choisi?.Nom ?? "";
        _magasinAdresse = choisi?.Adresse;
        _magasinTelephone = choisi?.Telephone;
    }

    private async Task ChargerSessionAsync()
    {
        _caisses = await CaisseApi.GetCaissesAsync(_magasinId!.Value);
        _session = await CaisseApi.GetSessionActiveAsync(_magasinId.Value);

        if (_session != null)
        {
            _caisseLibelle = _caisses.FirstOrDefault(c => c.Id == _session.CaisseId)?.Libelle ?? "Caisse";
            await ChargerArticles();
        }
        else
        {
            _caisseChoisieId = _caisses.FirstOrDefault(c => !c.SessionOuverte)?.Id ?? Guid.Empty;
            if (ErreurCommerce.Derniere != null) Notif.Erreur(ErreurCommerce.Derniere ?? "Une erreur est survenue.", "Caisse");
        }
    }

    private async Task OuvrirSession()
    {
        _enCoursAction = true;
        try
        {
            var session = await CaisseApi.OuvrirSessionAsync(new OuvrirSessionPosRequest { CaisseId = _caisseChoisieId, FondDeCaisse = _fondDeCaisse });
            if (session == null) { Notif.Erreur(ErreurCommerce.Derniere ?? "Une erreur est survenue.", "Ouverture impossible"); return; }
            await ChargerSessionAsync();
        }
        finally { _enCoursAction = false; }
    }

    private async Task ChargerArticles()
    {
        if (_horsLigne) return;   // hors connexion : catalogue local
        _chargementArticles = true;
        var articles = await CatalogueApi.GetProduitsAsync(_magasinId);
        if (EstErreurReseau())
        {
            _horsLigne = true;    // on garde le catalogue déjà affiché
        }
        else
        {
            _articles = articles.Where(a => a.EstActif).ToList();
            await EnregistrerContexteLocalAsync();
        }
        FiltrerArticles();
        _chargementArticles = false;
    }

    // ── Scanner / recherche ──
    private void GererSaisieClavier(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_rechercheArticle))
        {
            var code = _rechercheArticle.Trim();
            var cible = TrouverParCode(code) ?? (_articlesFiltres.Count == 1 ? _articlesFiltres[0] : null);
            if (cible != null)
            {
                AjouterAuPanier(cible);
                _rechercheArticle = "";
                FiltrerArticles();
            }
            else
            {
                Notif.Avertir(_articlesFiltres.Count == 0 ? $"Aucun article : {code}" : "Plusieurs articles correspondent.", "Précisez l'article");
            }
        }
        else
        {
            FiltrerArticles();
        }
    }

    // Code-barres, ISBN ou référence (les QR des étiquettes Finama contiennent la référence)
    private ProduitCommerceDto? TrouverParCode(string code) => _articles.FirstOrDefault(a =>
        string.Equals(a.CodeBarres, code, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(a.Isbn, code, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(a.Reference, code, StringComparison.OrdinalIgnoreCase));

    // Caméra en mode continu : chaque code lu ajoute l'article au ticket
    private async Task SurCodeCamera(string code)
    {
        var article = TrouverParCode(code);
        if (article is null)
        {
            Notif.Avertir($"Aucun article pour le code {code}.", "Code inconnu");
            return;
        }
        AjouterAuPanier(article);
        await InvokeAsync(StateHasChanged);
    }

    private void FiltrerArticles()
    {
        _articlesFiltres = _articles.Where(a =>
            string.IsNullOrEmpty(_rechercheArticle) ||
            a.Designation.Contains(_rechercheArticle, StringComparison.OrdinalIgnoreCase) ||
            (a.CodeBarres != null && a.CodeBarres.Contains(_rechercheArticle, StringComparison.OrdinalIgnoreCase)) ||
            (a.Isbn != null && a.Isbn.Contains(_rechercheArticle, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    // ── Clavier ──
    private async Task GererRaccourcis(KeyboardEventArgs e)
    {
        if (e.Key == "F8" && _session != null && !_afficherPaiement) { await OuvrirTickets(); return; }
        if (e.Key == "F2" && _panier.Any() && !_afficherPaiement) OuvrirPaiement();
        else if (e.Key == "F4" && _dernierRecu != null && !_afficherPaiement) await ImprimerDernierTicket();
        else if (e.Key == "Escape")
        {
            if (_annulation != null) _annulation = null;
            else if (_afficherTickets) _afficherTickets = false;
            else if (_afficherPaiement) FermerPaiement();
            else if (_afficherCloture) _afficherCloture = false;
            else await DemanderViderPanier();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _arretSynchronisation.Cancel();
        try { if (_jsHorsLigne is not null) await _jsHorsLigne.DisposeAsync(); }
        catch { /* runtime JS arrêté */ }
        try { if (_jsPartage is not null) await _jsPartage.DisposeAsync(); }
        catch { /* runtime JS arrêté */ }
        try { if (_jsCodes is not null) await _jsCodes.DisposeAsync(); }
        catch { /* runtime JS arrêté */ }
        try { if (_jsPos is not null) await _jsPos.DisposeAsync(); }
        catch { /* runtime JS arrêté */ }
    }

}
