using Finama.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Finama.Web.Services;

public class CommerceApiService
{
    private readonly HttpClient _http;
    private readonly ILocalStorage _stockage;
    private readonly NavigationManager _nav;

    public CommerceApiService(HttpClient http, ILocalStorage stockage, NavigationManager nav)
    {
        _http = http;
        _stockage = stockage;
        _nav = nav;
    }

    /// <summary>Message de la dernière erreur (API, réseau, session). Null si le dernier appel a réussi.</summary>
    public string? DerniereErreur { get; private set; }

    // ─── Infrastructure ──────────────────────────────────────────────────────

    private async Task<string?> GetDeviceIdAsync() => await _stockage.LireAsync(ClesStockage.Appareil);
    private async Task<string?> GetTokenAsync() => await _stockage.LireAsync(ClesStockage.Jeton);

    public async Task<Guid?> GetMagasinActifIdAsync()
    {
        var valeur = await _stockage.LireAsync(ClesStockage.MagasinId);
        return Guid.TryParse(valeur, out var id) && id != Guid.Empty ? id : null;
    }

    // Jeton + X-Device-Id : ajoutés par AuthHeaderHandler (une seule source de vérité, refresh automatique).
    private Task SetAuthHeaderAsync() => Task.CompletedTask;

    private async Task<HttpResponseMessage?> EnvoyerAsync(HttpMethod methode, string url, object? corps = null)
    {
        DerniereErreur = null;
        try
        {
            await SetAuthHeaderAsync();
            using var requete = new HttpRequestMessage(methode, url);
            if (corps != null) requete.Content = JsonContent.Create(corps, corps.GetType());

            var reponse = await _http.SendAsync(requete);

            if (reponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                DerniereErreur = "Session expirée. Veuillez vous reconnecter.";
                _nav.NavigateTo("/connexion", forceLoad: true);
                return null;
            }

            if (!reponse.IsSuccessStatusCode)
                DerniereErreur = await LireErreurAsync(reponse);

            return reponse;
        }
        catch (Exception)
        {
            DerniereErreur = MessageReseau;
            return null;
        }
    }

    private const string MessageReseau = "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.";
    private const string MessageServeur = "Une erreur est survenue sur le serveur. Réessayez dans un instant ; si le problème persiste, contactez le support.";

    private static async Task<string> LireErreurAsync(HttpResponseMessage reponse)
    {
        // Erreur serveur (5xx) : le corps peut contenir des détails techniques, on ne l'affiche jamais.
        if ((int)reponse.StatusCode >= 500) return MessageServeur;
        if (reponse.StatusCode == HttpStatusCode.Forbidden) return "Vous n'avez pas l'autorisation d'effectuer cette action.";

        var texte = await reponse.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(texte)) return $"Erreur {(int)reponse.StatusCode}.";

        try
        {
            using var doc = JsonDocument.Parse(texte);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var nom in new[] { "message", "Message", "detail", "title" })
                {
                    if (doc.RootElement.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString() ?? texte;
                }
            }
        }
        catch { /* texte brut */ }

        // Texte brut non JSON (ex. page d'erreur HTML) : message générique plutôt que le contenu.
        return texte.TrimStart().StartsWith('<') || texte.Length > 300 ? $"La demande n'a pas abouti (erreur {(int)reponse.StatusCode})." : texte;
    }

    private static async Task<T?> LireAsync<T>(HttpResponseMessage? reponse)
    {
        if (reponse is not { IsSuccessStatusCode: true } || reponse.StatusCode == HttpStatusCode.NoContent) return default;
        return await reponse.Content.ReadFromJsonAsync<T>();
    }

    private static bool Ok(HttpResponseMessage? reponse) => reponse is { IsSuccessStatusCode: true };

    private async Task<List<T>> ListeAsync<T>(string url)
        => await LireAsync<List<T>>(await EnvoyerAsync(HttpMethod.Get, url)) ?? new();

    /// <summary>Injecte le magasin actif de la session si la requête n'en précise pas (sinon le serveur prend le principal).</summary>
    private async Task<JsonObject> AvecMagasinAsync(object requete)
    {
        var noeud = JsonSerializer.SerializeToNode(requete, requete.GetType(), JsonSerializerOptions.Web) as JsonObject ?? new JsonObject();
        var actuel = noeud["magasinId"]?.ToString();
        if (!Guid.TryParse(actuel, out var g) || g == Guid.Empty)
        {
            var actif = await GetMagasinActifIdAsync();
            if (actif.HasValue) noeud["magasinId"] = actif.Value.ToString();
        }
        return noeud;
    }

    // ─── CATALOGUE ───────────────────────────────────────────────────────────

    public async Task<List<ProduitCommerceDto>> GetProduitsAsync(Guid? magasinId = null)
    {
        magasinId ??= await GetMagasinActifIdAsync();
        var url = magasinId.HasValue ? $"api/commerce/produits?magasinId={magasinId}" : "api/commerce/produits";
        return await ListeAsync<ProduitCommerceDto>(url);
    }

    public async Task<bool> CreerProduitAsync(ProduitCommerceDto produit)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/produits", produit));

    public async Task<bool> ModifierProduitAsync(Guid id, ProduitCommerceDto produit)
        => Ok(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/produits/{id}", produit));

    public async Task<bool> DesactiverProduitAsync(Guid id)
        => Ok(await EnvoyerAsync(HttpMethod.Delete, $"api/commerce/produits/{id}"));

    // ─── STOCKS & LOTS ───────────────────────────────────────────────────────

    public async Task<List<StockLotDto>> GetStocksLotsAsync()
    {
        var magasinId = await GetMagasinActifIdAsync();
        var url = magasinId.HasValue ? $"api/commerce/stock/lots?magasinId={magasinId}" : "api/commerce/stock/lots";
        return await ListeAsync<StockLotDto>(url);
    }

    public async Task<(bool Succes, string MessageErreur)> EnregistrerEntreeInitialeAsync(object request)
    {
        var reponse = await EnvoyerAsync(HttpMethod.Post, "api/commerce/stock/entree-initiale", await AvecMagasinAsync(request));
        return Ok(reponse) ? (true, string.Empty) : (false, DerniereErreur ?? "Erreur inconnue du serveur.");
    }

    public async Task<bool> AjusterStockAsync(object request)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/stock/ajuster", await AvecMagasinAsync(request)));

    // ─── INVENTAIRES ─────────────────────────────────────────────────────────

    public async Task<List<InventaireDto>> GetInventairesAsync()
        => await ListeAsync<InventaireDto>("api/commerce/stock/inventaires");

    public async Task<InventaireDto?> OuvrirInventaireAsync(OuvrirInventaireRequest request)
    {
        if (request.MagasinId == Guid.Empty)
            request.MagasinId = await GetMagasinActifIdAsync() ?? Guid.Empty;
        return await LireAsync<InventaireDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/stock/inventaires/ouvrir", request));
    }

    public async Task<InventaireDto?> GetInventaireDetailsAsync(Guid id)
        => await LireAsync<InventaireDto>(await EnvoyerAsync(HttpMethod.Get, $"api/commerce/stock/inventaires/{id}"));

    public async Task<bool> CompterInventaireAsync(Guid inventaireId, object request)
    => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/stock/inventaires/{inventaireId}/compter", request));
    public async Task<bool> CloturerInventaireAsync(Guid inventaireId, object request)
    => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/stock/inventaires/{inventaireId}/cloturer", request));

    public async Task<bool> AnnulerInventaireAsync(Guid inventaireId)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/stock/inventaires/{inventaireId}/annuler"));

    // ─── TABLEAU DE BORD ─────────────────────────────────────────────────────

    public async Task<TableauBordCommerceDto?> GetTableauBordAsync()
        => await LireAsync<TableauBordCommerceDto>(await EnvoyerAsync(HttpMethod.Get, "api/commerce/tableau-bord"));

    // ─── PARAMÈTRES & PROFILS D'ACTIVITÉ ─────────────────────────────────────

    public async Task<ParametresCommerceDto?> GetParametresAsync()
        => await LireAsync<ParametresCommerceDto>(await EnvoyerAsync(HttpMethod.Get, "api/commerce/parametres"));

    public async Task<ParametresCommerceDto?> SauvegarderParametresAsync(ParametresCommerceDto parametres)
        => await LireAsync<ParametresCommerceDto>(await EnvoyerAsync(HttpMethod.Put, "api/commerce/parametres", parametres));

    public async Task<List<ProfilActiviteDto>> GetProfilsActiviteAsync()
        => await ListeAsync<ProfilActiviteDto>("api/commerce/parametres/profils");

    public async Task<ParametresCommerceDto?> AppliquerProfilAsync(string code)
        => await LireAsync<ParametresCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/parametres/appliquer-profil", new { code }));

    // ─── CATÉGORIES & ÉDITEURS ───────────────────────────────────────────────

    public async Task<List<CategorieCommerceDto>> GetCategoriesAsync()
        => await ListeAsync<CategorieCommerceDto>("api/commerce/categories");

    public async Task<CategorieCommerceDto?> CreerCategorieAsync(SaveCategorieCommerceRequest request)
        => await LireAsync<CategorieCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/categories", request));

    public async Task<CategorieCommerceDto?> ModifierCategorieAsync(Guid id, SaveCategorieCommerceRequest request)
        => await LireAsync<CategorieCommerceDto>(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/categories/{id}", request));

    public async Task<bool> SupprimerCategorieAsync(Guid id)
        => Ok(await EnvoyerAsync(HttpMethod.Delete, $"api/commerce/categories/{id}"));

    public async Task<List<EditeurCommerceDto>> GetEditeursAsync()
        => await ListeAsync<EditeurCommerceDto>("api/commerce/editeurs");

    public async Task<EditeurCommerceDto?> CreerEditeurAsync(SaveEditeurCommerceRequest request)
        => await LireAsync<EditeurCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/editeurs", request));

    public async Task<EditeurCommerceDto?> ModifierEditeurAsync(Guid id, SaveEditeurCommerceRequest request)
        => await LireAsync<EditeurCommerceDto>(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/editeurs/{id}", request));

    public async Task<bool> DefinirEditeurActifAsync(Guid id, bool actif)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/editeurs/{id}/actif?actif={actif.ToString().ToLower()}"));

    // ─── MAGASINS ────────────────────────────────────────────────────────────

    public async Task<List<MagasinDto>> GetMagasinsAsync()
        => await ListeAsync<MagasinDto>("api/commerce/magasins");

    public async Task<bool> CreerMagasinAsync(CreerMagasinRequest request)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/magasins", request));

    public async Task<bool> DefinirMagasinPrincipalAsync(Guid id)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/magasins/{id}/principal"));

    // ─── CAISSE (POS) ────────────────────────────────────────────────────────

    public async Task<List<CaissePosDto>> GetCaissesAsync(Guid magasinId)
        => await ListeAsync<CaissePosDto>($"api/commerce/pos/caisses?magasinId={magasinId}");

    public async Task<SessionPosDto?> GetSessionActiveAsync(Guid magasinId)
        => await LireAsync<SessionPosDto>(await EnvoyerAsync(HttpMethod.Get, $"api/commerce/pos/session-active?magasinId={magasinId}"));

    public async Task<SessionPosDto?> OuvrirSessionAsync(OuvrirSessionPosRequest request)
        => await LireAsync<SessionPosDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/pos/sessions/ouvrir", request));

    public async Task<TicketPosDto?> EncaisserVenteAsync(CreerVentePosRequest request)
        => await LireAsync<TicketPosDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/pos/ventes", request));

    public async Task<bool> CloturerSessionAsync(CloturerSessionPosRequest request)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/pos/sessions/cloturer", request));

    // ─── CLIENTS ─────────────────────────────────────────────────────────────

    public async Task<List<ClientCommerceDto>> RechercherClientsAsync(string? recherche = null)
    {
        var url = string.IsNullOrWhiteSpace(recherche)
            ? "api/commerce/clients"
            : $"api/commerce/clients?recherche={Uri.EscapeDataString(recherche)}";
        return await ListeAsync<ClientCommerceDto>(url);
    }

    public async Task<ClientDetailCommerceDto?> GetClientAsync(Guid id)
        => await LireAsync<ClientDetailCommerceDto>(await EnvoyerAsync(HttpMethod.Get, $"api/commerce/clients/{id}"));

    public async Task<ClientCommerceDto?> CreerClientAsync(SaveClientCommerceRequest request)
        => await LireAsync<ClientCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/clients", request));

    public async Task<ClientCommerceDto?> ModifierClientAsync(Guid id, SaveClientCommerceRequest request)
        => await LireAsync<ClientCommerceDto>(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/clients/{id}", request));

    public async Task<bool> DefinirClientActifAsync(Guid id, bool actif)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/clients/{id}/actif?actif={actif.ToString().ToLower()}"));
}

// ─── DTOs Dédiés au Commerce ──────────────────────────────────────────────

public class ProduitCommerceDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";

    [JsonIgnore]
    public string Designation
    {
        get => Libelle;
        set => Libelle = value;
    }
    public string Libelle { get; set; } = "";

    public string? Description { get; set; }
    public int Type { get; set; }               // 0 Standard, 1 Vrac, 2 Livre, 3 Service
    public int? Unite { get; set; }             // 0 Pièce, 1 Kg, 2 g, 3 L, 4 m, 5 Heure
    public Guid? CategorieId { get; set; }
    public string? CategorieNom { get; set; }

    [JsonIgnore] public bool IsLivre => Type == 2;
    [JsonIgnore] public bool EstVrac => Type == 1;
    [JsonIgnore] public bool EstService => Type == 3;

    public decimal PrixAchat { get; set; }
    public decimal PrixVenteTTC { get; set; }

    [JsonIgnore]
    public decimal PrixVenteTtc
    {
        get => PrixVenteTTC;
        set => PrixVenteTTC = value;
    }

    public decimal TauxTva { get; set; } = 18m;
    public decimal StockActuel { get; set; }
    public decimal SeuilReappro { get; set; }

    [JsonIgnore]
    public decimal StockAlerte
    {
        get => SeuilReappro;
        set => SeuilReappro = value;
    }

    public bool SuiviLots { get; set; }
    public bool GereStock { get; set; } = true;
    public bool EstActif { get; set; } = true;
    public string? CodeBarres { get; set; }
    public string? Isbn { get; set; }
    public string? Auteur { get; set; }
    public string? Editeur { get; set; }        // lecture seule (nom) ; on envoie EditeurId
    public Guid? EditeurId { get; set; }
    public Dictionary<string, string> Attributs { get; set; } = new();   // valeurs des champs personnalisés de la catégorie
}

public class StockLotDto
{
    public Guid Id { get; set; }
    public Guid ProduitId { get; set; }
    public string DesignationProduit { get; set; } = "";
    public string NumeroLot { get; set; } = "";
    public DateTime? DateExpiration { get; set; }
    public decimal QuantiteDisponible { get; set; }
}

// ─── POS ──────────────────────────────────────────────────────────────────

public enum ModePaiementPos { Especes = 1, Carte = 2, MobileMoney = 3, Credit = 4, Points = 5 }

public class CaissePosDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public bool SessionOuverte { get; set; }
}

public class SessionPosDto
{
    public Guid Id { get; set; }
    public Guid CaisseId { get; set; }
    public Guid MagasinId { get; set; }
    public DateTime DateOuverture { get; set; }
    public decimal FondDeCaisse { get; set; }
}

public class OuvrirSessionPosRequest
{
    public Guid CaisseId { get; set; }
    public decimal FondDeCaisse { get; set; }
}

public class CreerVentePosRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid SessionCaisseId { get; set; }
    public Guid? ClientId { get; set; }
    public List<LigneVentePosDto> Lignes { get; set; } = new();
    public List<PaiementPosDto> Paiements { get; set; } = new();
    public AutorisationPosDto? Autorisation { get; set; }
}

public class LigneVentePosDto
{
    public Guid ProduitId { get; set; }
    public decimal Quantite { get; set; }
    public decimal? RemisePourcent { get; set; }
}

public class PaiementPosDto
{
    public ModePaiementPos Mode { get; set; }
    public decimal Montant { get; set; }
    public string? Operateur { get; set; }
    public string? Reference { get; set; }
}

public class AutorisationPosDto
{
    public Guid SuperviseurId { get; set; }
    public string? Pin { get; set; }
}

public class TicketPosDto
{
    public Guid Id { get; set; }
    public string NumeroTicket { get; set; } = "";
    public DateTime DateVente { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalRemise { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal RenduMonnaie { get; set; }
}

public class ClotureLignePosDto
{
    public ModePaiementPos Mode { get; set; }
    public decimal MontantDeclare { get; set; }
}

public class CloturerSessionPosRequest
{
    public Guid SessionCaisseId { get; set; }
    public List<ClotureLignePosDto> Lignes { get; set; } = new();
    public string? Commentaire { get; set; }
}

// ─── CLIENTS ──────────────────────────────────────────────────────────────

public enum TypeClientCommerce { Particulier = 1, Entreprise = 2 }

public class ClientCommerceDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Nom { get; set; } = "";
    public TypeClientCommerce Type { get; set; } = TypeClientCommerce.Particulier;
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Adresse { get; set; }
    public string? Nif { get; set; }
    public decimal PlafondCredit { get; set; }
    public decimal SoldeCredit { get; set; }
    public int PointsFidelite { get; set; }
    public bool EstActif { get; set; } = true;
}

public class SaveClientCommerceRequest
{
    public string Nom { get; set; } = "";
    public TypeClientCommerce Type { get; set; } = TypeClientCommerce.Particulier;
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Adresse { get; set; }
    public string? Nif { get; set; }
    public decimal PlafondCredit { get; set; }
}

public class ClientDetailCommerceDto
{
    public ClientCommerceDto Client { get; set; } = new();
    public decimal TotalAchats { get; set; }
    public List<AchatClientCommerceDto> DerniersAchats { get; set; } = new();
    public List<MouvementFideliteCommerceDto> Fidelite { get; set; } = new();
}

public class AchatClientCommerceDto
{
    public Guid VenteId { get; set; }
    public string NumeroTicket { get; set; } = "";
    public DateTime DateVente { get; set; }
    public decimal TotalTTC { get; set; }
}

public class MouvementFideliteCommerceDto
{
    public int Type { get; set; }               // 1 gain, 2 utilisation, 3 ajustement, 4 expiration
    public int Points { get; set; }
    public int SoldeApres { get; set; }
    public DateTime DateMouvement { get; set; }
    public string? Motif { get; set; }
}

// ─── CATÉGORIES & ÉDITEURS ────────────────────────────────────────────────

public class ChampPersonnaliseDto
{
    public string Nom { get; set; } = "";
    public string Type { get; set; } = "texte";          // texte | nombre | liste | date
    public bool Obligatoire { get; set; }
    public List<string>? Options { get; set; }
}

public class CategorieCommerceDto
{
    public Guid Id { get; set; }
    public string Libelle { get; set; } = "";
    public Guid? ParentId { get; set; }
    public int NbProduits { get; set; }
    public List<ChampPersonnaliseDto> Champs { get; set; } = new();
}

public class SaveCategorieCommerceRequest
{
    public string Libelle { get; set; } = "";
    public Guid? ParentId { get; set; }
    public List<ChampPersonnaliseDto> Champs { get; set; } = new();
}

public class TableauBordCommerceDto
{
    public bool ActiviteConfiguree { get; set; }
    public bool UtiliseLots { get; set; }
    public int NbMagasins { get; set; }
    public int NbPointsDeVente { get; set; }
    public int NbArticles { get; set; }
    public int NbClients { get; set; }
    public bool SessionOuverte { get; set; }
    public int NbVentesJour { get; set; }
    public decimal ChiffreAffairesJour { get; set; }
    public int ArticlesSousSeuil { get; set; }
    public int LotsPerimes { get; set; }
    public int LotsProchesPeremption { get; set; }
    public decimal CreancesClients { get; set; }
    public int NbClientsEnCredit { get; set; }
}

public class ParametresCommerceDto
{
    public string? Activite { get; set; }
    public bool UtiliseLots { get; set; }
    public bool UtiliseVrac { get; set; }
    public bool UtiliseLivres { get; set; }
    public decimal TauxTvaDefaut { get; set; } = 18;
    public decimal MontantParPoint { get; set; }
    public decimal ValeurPointFcfa { get; set; }
    public decimal SeuilRemiseSansPinPourcent { get; set; }
    public decimal RemiseMaxPourcent { get; set; }
    public bool AutoriserStockNegatif { get; set; }
    public string PrefixeTicket { get; set; } = "T";
}

public class ProfilActiviteDto
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public string Description { get; set; } = "";
    public bool UtiliseLots { get; set; }
    public bool UtiliseVrac { get; set; }
    public bool UtiliseLivres { get; set; }
    public List<string> CategoriesTypes { get; set; } = new();
}

public class EditeurCommerceDto
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Contact { get; set; }
    public bool EstActif { get; set; } = true;
    public int NbTitres { get; set; }
}

public class SaveEditeurCommerceRequest
{
    public string Nom { get; set; } = "";
    public string? Contact { get; set; }
}

// ─── DTOs pour les Inventaires ────────────────────────────────────────────

public class OuvrirInventaireRequest
{
    public Guid MagasinId { get; set; } = Guid.Empty;
    public string Libelle { get; set; } = "Inventaire Général";
    public Guid? CategorieId { get; set; }

    public OuvrirInventaireRequest() { }

    public OuvrirInventaireRequest(Guid magasinId, string libelle, Guid? categorieId)
    {
        MagasinId = magasinId;
        Libelle = libelle;
        CategorieId = categorieId;
    }
}

public class InventaireLigneDto
{
    public Guid LigneId { get; set; }
    public Guid ProduitId { get; set; }
    public string ProduitLibelle { get; set; } = "";
    public Guid? LotId { get; set; }
    public string? NumeroLot { get; set; }
    public decimal? QuantiteTheorique { get; set; }
    public decimal? QuantiteComptee { get; set; }
    public decimal? Ecart { get; set; }

    public InventaireLigneDto() { }

    public InventaireLigneDto(
        Guid ligneId,
        Guid produitId,
        string produitLibelle,
        Guid? lotId,
        string? numeroLot,
        decimal? quantiteTheorique,
        decimal? quantiteComptee,
        decimal? ecart)
    {
        LigneId = ligneId;
        ProduitId = produitId;
        ProduitLibelle = produitLibelle;
        LotId = lotId;
        NumeroLot = numeroLot;
        QuantiteTheorique = quantiteTheorique;
        QuantiteComptee = quantiteComptee;
        Ecart = ecart;
    }
}

public record InventaireDto(
    Guid Id,
    string Numero,
    string Libelle,
    StatutInventaire Statut,
    DateTime DateOuverture,
    DateTime? DateCloture,
    IReadOnlyList<InventaireLigneDto> Lignes
);

public record SaisieLigneComptage(Guid LigneId, decimal QuantiteComptee);
public record ComptageRequest(List<SaisieLigneComptage> Lignes);
public record ClotureInventaireRequest(bool TraiterNonComptesCommeZero);

public enum TypeMagasin { PointDeVente = 1, Entrepot = 2, Transit = 3 }

public record MagasinDto(
    Guid Id,
    string Code,
    string Nom,
    TypeMagasin Type,
    string? Adresse,
    string? Telephone,
    string? Responsable,
    bool EstPrincipal,
    bool EstActif
);

public class CreerMagasinRequest
{
    public string Code { get; set; } = "";
    public string Nom { get; set; } = "";
    public TypeMagasin Type { get; set; } = TypeMagasin.PointDeVente;
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Responsable { get; set; }
    public bool EstPrincipal { get; set; }
}

public enum StatutInventaire
{
    Ouvert = 0,
    Cloture = 1,
    Annule = 2
}
