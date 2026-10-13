using System.Net.Http.Json;
using Finama.Web.Models;

namespace Finama.Web.Services;

public interface ITenantAdminService
{
    // Entreprises
    Task<List<TenantAdminDto>> ObtenirTenantsAsync();
    Task<DashboardStatsDto?> ObtenirStatsAsync();
    Task<ResultatApi> ChangerStatutTenantAsync(Guid tenantId, bool actif);
    Task<ResultatApi> ActiverAbonnementAsync(Guid tenantId, ActiverAbonnementRequest request);
    Task<ResultatApi> InitialiserComptabiliteAsync(Guid tenantId);

    // Utilisateurs
    Task<List<UtilisateurAdminDto>> ObtenirUtilisateursAsync(Guid tenantId);
    Task<ResultatApi> ChangerStatutUtilisateurAsync(Guid utilisateurId, bool actif);
    Task<ResultatApi> ReinitialiserMotDePasseAsync(Guid utilisateurId);
    Task<ResultatApi> SupprimerUtilisateurAsync(Guid utilisateurId);

    // Demandes d'accès
    Task<List<DemandeAccesResponseDto>> ObtenirDemandesAsync();
    Task<ResultatApi> ApprouverDemandeAsync(Guid demandeId);
    Task<ResultatApi> RefuserDemandeAsync(Guid demandeId);
}

public class TenantAdminService : ITenantAdminService
{
    private const string Base = "api/superadmin";
    private readonly HttpClient _http;

    public TenantAdminService(HttpClient http) => _http = http;

    public async Task<List<TenantAdminDto>> ObtenirTenantsAsync()
        => await _http.GetFromJsonAsync<List<TenantAdminDto>>($"{Base}/tenants") ?? new();

    public async Task<DashboardStatsDto?> ObtenirStatsAsync()
        => await _http.GetFromJsonAsync<DashboardStatsDto>($"{Base}/stats");

    public async Task<ResultatApi> ChangerStatutTenantAsync(Guid tenantId, bool actif)
        => await LireAsync(await _http.PutAsJsonAsync($"{Base}/tenants/{tenantId}/statut",
            new ChangerStatutTenantRequest { EstActif = actif }), "Statut mis à jour.");

    public async Task<ResultatApi> ActiverAbonnementAsync(Guid tenantId, ActiverAbonnementRequest request)
        => await LireAsync(await _http.PostAsJsonAsync($"{Base}/tenants/{tenantId}/abonnement", request),
            "Abonnement activé.");

    public async Task<ResultatApi> InitialiserComptabiliteAsync(Guid tenantId)
        => await LireAsync(await _http.PostAsync($"{Base}/tenants/{tenantId}/initialiser-comptabilite", null),
            "Comptabilité initialisée.");

    public async Task<List<UtilisateurAdminDto>> ObtenirUtilisateursAsync(Guid tenantId)
        => await _http.GetFromJsonAsync<List<UtilisateurAdminDto>>($"{Base}/tenants/{tenantId}/utilisateurs") ?? new();

    public async Task<ResultatApi> ChangerStatutUtilisateurAsync(Guid utilisateurId, bool actif)
        => await LireAsync(await _http.PutAsync($"{Base}/utilisateurs/{utilisateurId}/statut?actif={actif.ToString().ToLower()}", null),
            "Statut de l'utilisateur mis à jour.");

    public async Task<ResultatApi> ReinitialiserMotDePasseAsync(Guid utilisateurId)
        => await LireAsync(await _http.PostAsync($"{Base}/utilisateurs/{utilisateurId}/reinitialiser-mot-de-passe", null),
            "Mot de passe réinitialisé.");

    public async Task<ResultatApi> SupprimerUtilisateurAsync(Guid utilisateurId)
        => await LireAsync(await _http.DeleteAsync($"{Base}/utilisateurs/{utilisateurId}"), "Utilisateur supprimé.");

    public async Task<List<DemandeAccesResponseDto>> ObtenirDemandesAsync()
        => await _http.GetFromJsonAsync<List<DemandeAccesResponseDto>>($"{Base}/demandes-acces") ?? new();

    public async Task<ResultatApi> ApprouverDemandeAsync(Guid demandeId)
        => await LireAsync(await _http.PostAsync($"{Base}/demandes-acces/{demandeId}/approuver", null),
            "Demande approuvée.");

    public async Task<ResultatApi> RefuserDemandeAsync(Guid demandeId)
        => await LireAsync(await _http.PostAsync($"{Base}/demandes-acces/{demandeId}/refuser", null),
            "Demande refusée.");

    // Lit le message renvoyé par l'API ({ message } ou { Message }) en succès comme en erreur.
    private static async Task<ResultatApi> LireAsync(HttpResponseMessage reponse, string messageParDefaut)
    {
        string? message = null;
        try { message = (await reponse.Content.ReadFromJsonAsync<MessageDto>())?.Message; }
        catch { /* corps vide (204) ou non JSON */ }

        return reponse.IsSuccessStatusCode
            ? new ResultatApi(true, message ?? messageParDefaut)
            : new ResultatApi(false, message ?? $"L'opération a échoué (code {(int)reponse.StatusCode}).");
    }

    private record MessageDto(string? Message);
}
