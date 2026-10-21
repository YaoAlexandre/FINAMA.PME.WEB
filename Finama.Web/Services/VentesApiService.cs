using System.Net.Http.Json;
using System.Text.Json;

namespace Finama.Web.Services;

/// <summary>Module Ventes &amp; Devis : devis, tableau de bord, clients (Tiers). Erreurs : DerniereErreur.</summary>
public class VentesApiService
{
    private readonly HttpClient _http;

    public VentesApiService(HttpClient http) => _http = http;

    public string? DerniereErreur { get; private set; }

    // ─── Devis ───────────────────────────────────────────────────────────────
    public Task<List<DevisResumeVm>?> ListerDevisAsync(int? statut = null, string? recherche = null)
    {
        var url = "api/devis?" + (statut is { } s ? $"statut={s}&" : "") +
                  (string.IsNullOrWhiteSpace(recherche) ? "" : $"recherche={Uri.EscapeDataString(recherche)}");
        return LireAsync<List<DevisResumeVm>>(HttpMethod.Get, url);
    }

    public Task<VentesTableauBordVm?> TableauBordAsync() => LireAsync<VentesTableauBordVm>(HttpMethod.Get, "api/devis/tableau-bord");
    public Task<DevisVm?> ObtenirAsync(Guid id) => LireAsync<DevisVm>(HttpMethod.Get, $"api/devis/{id}");
    public Task<DevisVm?> CreerAsync(DevisSaisie d) => LireAsync<DevisVm>(HttpMethod.Post, "api/devis", d);
    public Task<DevisVm?> ModifierAsync(Guid id, DevisSaisie d) => LireAsync<DevisVm>(HttpMethod.Put, $"api/devis/{id}", d);
    public Task<DevisVm?> EnvoyerAsync(Guid id) => LireAsync<DevisVm>(HttpMethod.Post, $"api/devis/{id}/envoyer");
    public Task<DevisVm?> AccepterAsync(Guid id) => LireAsync<DevisVm>(HttpMethod.Post, $"api/devis/{id}/accepter");
    public Task<DevisVm?> RefuserAsync(Guid id, string? motif) => LireAsync<DevisVm>(HttpMethod.Post, $"api/devis/{id}/refuser", new { Motif = motif });
    public Task<DevisVm?> RemettreEnBrouillonAsync(Guid id) => LireAsync<DevisVm>(HttpMethod.Post, $"api/devis/{id}/brouillon");
    public Task<DevisVm?> DupliquerAsync(Guid id) => LireAsync<DevisVm>(HttpMethod.Post, $"api/devis/{id}/dupliquer");
    public Task<ConversionVm?> ConvertirAsync(Guid id) => LireAsync<ConversionVm>(HttpMethod.Post, $"api/devis/{id}/convertir");

    public async Task<bool> SupprimerAsync(Guid id)
        => await EnvoyerAsync(HttpMethod.Delete, $"api/devis/{id}") is { IsSuccessStatusCode: true };

    // ─── Clients (Tiers comptables, fiche client unique) ─────────────────────
    public async Task<List<ClientVm>> RechercherClientsAsync(string? q)
    {
        var url = "api/tiers/recherche" + (string.IsNullOrWhiteSpace(q) ? "" : $"?q={Uri.EscapeDataString(q.Trim())}");
        var liste = await LireAsync<List<ClientVm>>(HttpMethod.Get, url) ?? new();
        return liste.Where(c => !c.EstFournisseur).ToList();   // pas de fournisseurs dans un devis
    }

    public Task<ClientVm?> CreerClientAsync(NouveauClient c)
        => LireAsync<ClientVm>(HttpMethod.Post, "api/Tiers", new { c.Nom, Type = 0, c.Telephone, c.Email, c.Adresse, NINEA = c.Nif });

    // ─── Plomberie ───────────────────────────────────────────────────────────
    private async Task<T?> LireAsync<T>(HttpMethod m, string url, object? corps = null)
    {
        var r = await EnvoyerAsync(m, url, corps);
        if (r is not { IsSuccessStatusCode: true }) return default;
        try { return await r.Content.ReadFromJsonAsync<T>(); }
        catch { DerniereErreur = "Réponse inattendue du serveur."; return default; }
    }

    private async Task<HttpResponseMessage?> EnvoyerAsync(HttpMethod m, string url, object? corps = null)
    {
        DerniereErreur = null;
        try
        {
            using var req = new HttpRequestMessage(m, url);
            if (corps is not null) req.Content = JsonContent.Create(corps, corps.GetType());
            var r = await _http.SendAsync(req);
            if (!r.IsSuccessStatusCode) DerniereErreur = await MessageAsync(r);
            return r;
        }
        catch
        {
            DerniereErreur = "Serveur injoignable. Vérifiez votre connexion.";
            return null;
        }
    }

    private static async Task<string> MessageAsync(HttpResponseMessage r)
    {
        if ((int)r.StatusCode >= 500) return "Erreur du serveur. Réessayez dans un instant.";
        try
        {
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("message", out var m) && m.GetString() is { Length: > 0 } s) return s;
        }
        catch { /* corps non JSON */ }
        return r.StatusCode == System.Net.HttpStatusCode.Forbidden
            ? "Vous n'avez pas l'autorisation d'effectuer cette action."
            : $"La demande n'a pas abouti (erreur {(int)r.StatusCode}).";
    }
}

// ─── Modèles (alignés sur les DTO de l'API) ───────────────────────────────────

public enum StatutDevisVm { Brouillon = 0, Envoye = 1, Accepte = 2, Refuse = 3, Expire = 4, Converti = 5 }

public record DevisResumeVm(Guid Id, string Numero, string Libelle, DateTime DateCreation, DateTime? DateExpiration,
    StatutDevisVm Statut, string StatutLibelle, string TiersNom, decimal TotalHT, decimal TotalTTC, string CreeParNom, string? FactureNumero);

public record LigneDevisVm(Guid Id, int Ordre, Guid? ProduitId, string Designation, decimal Quantite, decimal PrixUnitaireHT,
    decimal RemisePourcent, decimal TauxTVA, decimal MontantHT, decimal MontantTVA, decimal MontantTTC);

public record DevisVm(Guid Id, string Numero, string Libelle, DateTime DateCreation, DateTime? DateExpiration,
    StatutDevisVm Statut, string StatutLibelle, Guid TiersId, string TiersNom, string? TiersEmail, string? TiersTelephone,
    decimal SousTotalHT, decimal RemiseGlobalePourcent, decimal RemiseGlobale, decimal TotalHT, decimal TotalTVA, decimal TotalTTC,
    string? Notes, string? Conditions, DateTime? DateEnvoi, DateTime? DateDecision, string? MotifRefus,
    Guid? FactureId, string? FactureNumero, int Version, string CreeParNom, List<LigneDevisVm> Lignes);

public record VentesTableauBordVm(int NbBrouillons, int NbEnvoyes, decimal MontantEnAttente, int NbAcceptesNonFactures,
    decimal MontantAcceptesNonFactures, decimal TauxTransformation, decimal CaDevisFacturesMois, int NbExpirentSous7Jours,
    List<DevisResumeVm> AExpirer, List<DevisResumeVm> Derniers);

public record ConversionVm(Guid FactureId, string FactureNumero);

/// <summary>Type renvoyé tantôt en nombre (recherche), tantôt en texte (création) : lu de façon tolérante.</summary>
public record ClientVm(Guid Id, string Code, string Nom, JsonElement Type, string? Email, string? Telephone)
{
    public bool EstFournisseur => Type.ValueKind switch
    {
        JsonValueKind.Number => Type.GetInt32() == 1,
        JsonValueKind.String => string.Equals(Type.GetString(), "Fournisseur", StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}

public class NouveauClient
{
    public string Nom { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Adresse { get; set; }
    public string? Nif { get; set; }
}

/// <summary>Saisie d'un devis (formulaire) — envoyée telle quelle à l'API (CreerDevisRequest).</summary>
public class DevisSaisie
{
    public string Libelle { get; set; } = "";
    public Guid TiersId { get; set; }
    public DateTime? DateExpiration { get; set; } = DateTime.Today.AddDays(30);
    public string? Notes { get; set; }
    public string? Conditions { get; set; }
    public decimal RemiseGlobalePourcent { get; set; }
    public List<LigneSaisie> Lignes { get; set; } = new();

    public decimal SousTotalHT => Math.Round(Lignes.Sum(l => l.MontantHT), 2);
    public decimal RemiseGlobale => Math.Round(SousTotalHT * RemiseGlobalePourcent / 100m, 2);
    public decimal TotalHT => SousTotalHT - RemiseGlobale;
    public decimal TotalTVA => Math.Round(Lignes.Sum(l => Math.Round(l.MontantHT * (1 - RemiseGlobalePourcent / 100m) * l.TauxTVA / 100m, 2)), 2);
    public decimal TotalTTC => TotalHT + TotalTVA;
}

public class LigneSaisie
{
    public Guid? ProduitId { get; set; }
    public string Designation { get; set; } = "";
    public decimal Quantite { get; set; } = 1;
    public decimal PrixUnitaireHT { get; set; }
    public decimal RemisePourcent { get; set; }
    public decimal TauxTVA { get; set; } = 18;

    public decimal MontantHT => Math.Round(Quantite * PrixUnitaireHT * (1 - RemisePourcent / 100m), 2);
}
