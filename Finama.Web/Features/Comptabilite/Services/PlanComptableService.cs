using System.Net.Http.Json;
using Finama.Web.Core.Securite;

namespace Finama.Web.Features.Comptabilite.Services;

public interface IPlanComptableService
{
    Task<PagedResultDto<CompteComptableDto>?> GetPlanComptableAsync(string? classe = null, string? recherche = null, int page = 1, int pageSize = 100);
    Task<CompteComptableDto?> CreerCompteAsync(CreerCompteRequest request);
    Task<CompteComptableDto?> ModifierCompteAsync(Guid id, ModifierCompteRequest request);
    Task ChangerStatutCompteAsync(Guid id, bool estActif);
    Task<List<CompteSelectDto>> GetPlanComptableSelectAsync(string? classe = null);
    Task<string> GetProchainNumeroSousCompteAsync(Guid parentId);
    Task<List<CompteComptableDto>> ListerComptesComptablesAsync(string? classe = null);
    Task<List<ClasseComptableModel>> GetClassesComptablesAsync();
    Task<bool> CreerClasseComptableAsync(ClasseComptableModel model);
    Task<bool> ModifierClasseComptableAsync(int numero, string nouveauLibelle);
}

public sealed class PlanComptableService : IPlanComptableService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public PlanComptableService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    // ─── Plan comptable ───────────────────────────────────────────────────────
    public async Task<PagedResultDto<CompteComptableDto>?> GetPlanComptableAsync(
        string? classe = null, string? recherche = null, int page = 1, int pageSize = 100)
    {
        await _appareil.GarantirAsync();
        var url = $"api/PlanComptable?page={page}&pageSize={pageSize}";

        if (!string.IsNullOrEmpty(classe)) url += $"&classe={classe}";
        if (!string.IsNullOrEmpty(recherche)) url += $"&recherche={Uri.EscapeDataString(recherche)}";

        return await _http.GetFromJsonAsync<PagedResultDto<CompteComptableDto>>(url);
    }

    public async Task<CompteComptableDto?> CreerCompteAsync(CreerCompteRequest request)
    {
        await _appareil.GarantirAsync();
        var r = await _http.PostAsJsonAsync("api/PlanComptable", request);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CompteComptableDto>();
    }

    public async Task<CompteComptableDto?> ModifierCompteAsync(Guid id, ModifierCompteRequest request)
    {
        await _appareil.GarantirAsync();
        var r = await _http.PutAsJsonAsync($"api/PlanComptable/{id}", request);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CompteComptableDto>();
    }

    public async Task ChangerStatutCompteAsync(Guid id, bool estActif)
    {
        await _appareil.GarantirAsync();
        var r = await _http.PatchAsJsonAsync($"api/PlanComptable/{id}/statut", new { estActif });
        r.EnsureSuccessStatusCode();
    }

    public async Task<List<CompteSelectDto>> GetPlanComptableSelectAsync(string? classe = null)
    {
        await _appareil.GarantirAsync();
        var url = "api/PlanComptable/select";
        if (!string.IsNullOrEmpty(classe)) url += $"?classe={classe}";

        return await _http.GetFromJsonAsync<List<CompteSelectDto>>(url) ?? new();
    }
    public async Task<string> GetProchainNumeroSousCompteAsync(Guid parentId)
    {
        // Appelle le endpoint du controlleur lié à la méthode backend qu'on a implémenté
        return await _http.GetStringAsync($"api/plan-comptable/parent/{parentId}/prochain-numero");
    }
    /// <summary>
    /// Récupère la liste des comptes comptables (filtrable par classe, ex: classe 4 pour les tiers)
    /// </summary>
    public async Task<List<CompteComptableDto>> ListerComptesComptablesAsync(string? classe = null)
    {
        try
        {
            // 🌟 AJOUT INDISPENSABLE : Attacher le token JWT et le Device-Id
            await _appareil.GarantirAsync();

            // Construit l'URL avec le paramètre de requête si fourni
            var url = string.IsNullOrEmpty(classe)
                ? "api/PlanComptable/select"
                : $"api/PlanComptable/select?classe={classe}";

            var comptes = await _http.GetFromJsonAsync<List<CompteComptableDto>>(url);
            return comptes ?? new List<CompteComptableDto>();
        }
        catch (Exception)
        {
            return new List<CompteComptableDto>();
        }
    }

    // ─── Classes Comptables ─────────────────────────────────────────────────────
    public async Task<List<ClasseComptableModel>> GetClassesComptablesAsync()
    {
        await _appareil.GarantirAsync();
        return await _http.GetFromJsonAsync<List<ClasseComptableModel>>("api/ClassesComptables") ?? new();
    }

    public async Task<bool> CreerClasseComptableAsync(ClasseComptableModel model)
    {
        await _appareil.GarantirAsync();
        var response = await _http.PostAsJsonAsync("api/ClassesComptables", model);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ModifierClasseComptableAsync(int numero, string nouveauLibelle)
    {
        await _appareil.GarantirAsync();
        var response = await _http.PutAsJsonAsync($"api/ClassesComptables/{numero}", nouveauLibelle);
        return response.IsSuccessStatusCode;
    }

}
