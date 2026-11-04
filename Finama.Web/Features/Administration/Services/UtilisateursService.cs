using System.Net.Http.Json;
using Finama.Web.Core.Securite;
using Finama.Web.Models;

namespace Finama.Web.Features.Administration.Services;

public interface IUtilisateursService
{
    Task<List<UtilisateurDto>?> GetUtilisateursTenantAsync();
    Task<string?> CreerCollaborateurAsync(CreerCollaborateurRequest model);
    Task<ResultatApi> ModifierStatutUtilisateurAsync(Guid id, bool actif);
    Task<ReinitialisationResultat> ReinitialiserMotDePasseUtilisateurAsync(Guid id);
}

public sealed class UtilisateursService : IUtilisateursService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public UtilisateursService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    // ─── Utilisateurs & Collaborateurs ──────────────────────────────────────────
    public async Task<List<UtilisateurDto>?> GetUtilisateursTenantAsync()
    {
        try
        {
            await _appareil.GarantirAsync();
            var response = await _http.GetFromJsonAsync<List<UtilisateurDto>>("api/Utilisateurs");
            return response ?? [];
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<string?> CreerCollaborateurAsync(CreerCollaborateurRequest model)
    {
        await _appareil.GarantirAsync();
        var response = await _http.PostAsJsonAsync("api/Utilisateurs", model);

        if (response.IsSuccessStatusCode) return null; // Pas d'erreur

        // Si on arrive ici, il y a une erreur : on lit le message renvoyé par l'API
        var errorData = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return errorData != null && errorData.ContainsKey("message")
            ? errorData["message"]
            : "Une erreur est survenue.";
    }


    public async Task<ResultatApi> ModifierStatutUtilisateurAsync(Guid id, bool actif)
    {
        await _appareil.GarantirAsync();
        var r = await _http.PutAsJsonAsync($"api/Utilisateurs/{id}/statut", actif);
        string? message = null;
        try { message = (await r.Content.ReadFromJsonAsync<ReponseMessage>())?.Message; } catch { }
        return new ResultatApi(r.IsSuccessStatusCode,
            message ?? (r.IsSuccessStatusCode ? (actif ? "Accès réactivé." : "Accès suspendu.") : "L'opération a échoué."));
    }

    public async Task<ReinitialisationResultat> ReinitialiserMotDePasseUtilisateurAsync(Guid id)
    {
        await _appareil.GarantirAsync();
        var r = await _http.PostAsync($"api/Utilisateurs/{id}/reinitialiser-mot-de-passe", null);
        ReponseReset? d = null;
        try { d = await r.Content.ReadFromJsonAsync<ReponseReset>(); } catch { }
        return new ReinitialisationResultat(r.IsSuccessStatusCode,
            d?.Message ?? (r.IsSuccessStatusCode ? "Mot de passe réinitialisé." : "L'opération a échoué."),
            d?.MotDePasseTemporaire);
    }

    private record ReponseMessage(string? Message);
    private record ReponseReset(string? Message, string? MotDePasseTemporaire);

}
