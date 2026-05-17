using System.Net.Http.Json;
using System.Net.Http.Headers;
using Finama.Web.Models;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components; // 🌟 AJOUT : Requis pour NavigationManager

namespace Finama.Web.Services;

public class FinamaApiService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly NavigationManager _nav; // 🌟 CORRECTION : Déclaration du champ de navigation

    // 🌟 CORRECTION : Injection de NavigationManager dans le constructeur
    public FinamaApiService(HttpClient http, IJSRuntime js, NavigationManager nav)
    {
        _http = http;
        _js = js;
        _nav = nav;
    }

    // ─── Auth ─────────────────────────────────────────────────────────────────
    public async Task<AuthResponse?> LoginAsync(string email, string motDePasse)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login", new { email, motDePasse });
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result is not null)
                await SauvegarderTokenAsync(result.AccessToken, result.NomUtilisateur,
                    result.NomEntreprise, result.DeviseSymbole);
            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task LogoutAsync()
    {
        await SetAuthHeaderAsync();
        await _http.PostAsync("api/auth/logout", null);
        await _js.InvokeVoidAsync("localStorage.removeItem", "finama_token");
        await _js.InvokeVoidAsync("localStorage.removeItem", "finama_user");
        await _js.InvokeVoidAsync("localStorage.removeItem", "finama_entreprise");
        await _js.InvokeVoidAsync("localStorage.removeItem", "finama_devise");
    }

    // ─── Tableau de bord ──────────────────────────────────────────────────────
    public async Task<TableauBordDto?> GetTableauBordAsync()
    {
        try
        {
            // 1. Attacher le jeton d'authentification local
            await SetAuthHeaderAsync();

            // 2. Envoyer la requête HTTP GET brute
            var response = await _http.GetAsync("api/TableauBord");

            // 3. 🌟 INTERCEPTION : Si le JWT est expiré ou invalide (Erreur 401)
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Déconnexion forcée (vide le localStorage)
                await LogoutAsync();

                // Redirection immédiate vers la page de connexion
                _nav.NavigateTo("/connexion", forceLoad: true);
                return null;
            }

            // 4. Si la requête a échoué pour une autre raison
            if (!response.IsSuccessStatusCode)
                return null;

            // 5. Tout est OK
            return await response.Content.ReadFromJsonAsync<TableauBordDto>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ─── Exercices ────────────────────────────────────────────────────────────
    public async Task<List<ExerciceDto>> GetExercicesAsync()
    {
        await SetAuthHeaderAsync();
        var res = await _http.GetFromJsonAsync<PagedResultDto<ExerciceDto>>("api/Exercices");
        return res?.Items ?? [];
    }

    public async Task<ExerciceDto?> GetExerciceCourantAsync()
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<ExerciceDto>("api/Exercices/courant");
    }

    // ─── Factures ─────────────────────────────────────────────────────────────
    public async Task<PagedResultDto<FactureDto>?> GetFacturesAsync(int page = 1, int pageSize = 20)
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<PagedResultDto<FactureDto>>(
            $"api/factures?page={page}&pageSize={pageSize}");
    }

    public async Task<FactureDto?> GetFactureAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<FactureDto>($"api/factures/{id}");
    }

    public async Task<FactureDto?> CreerFactureAsync(CreerFactureRequest request)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PostAsJsonAsync("api/factures", request);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<FactureDto>();
    }

    public async Task<byte[]?> GetFacturePdfAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        var r = await _http.GetAsync($"api/factures/{id}/pdf");
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadAsByteArrayAsync();
    }

    // ─── Plan comptable ───────────────────────────────────────────────────────
    public async Task<PagedResultDto<CompteComptableDto>?> GetPlanComptableAsync(
        string? classe = null, string? recherche = null, int page = 1, int pageSize = 100)
    {
        await SetAuthHeaderAsync();
        var url = $"api/PlanComptable?page={page}&pageSize={pageSize}";

        if (!string.IsNullOrEmpty(classe)) url += $"&classe={classe}";
        if (!string.IsNullOrEmpty(recherche)) url += $"&recherche={Uri.EscapeDataString(recherche)}";

        return await _http.GetFromJsonAsync<PagedResultDto<CompteComptableDto>>(url);
    }

    public async Task<CompteComptableDto?> CreerCompteAsync(CreerCompteRequest request)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PostAsJsonAsync("api/PlanComptable", request);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CompteComptableDto>();
    }

    public async Task<CompteComptableDto?> ModifierCompteAsync(Guid id, ModifierCompteRequest request)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PutAsJsonAsync($"api/PlanComptable/{id}", request);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CompteComptableDto>();
    }

    public async Task ChangerStatutCompteAsync(Guid id, bool estActif)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PatchAsJsonAsync($"api/PlanComptable/{id}/statut", new { estActif });
        r.EnsureSuccessStatusCode();
    }

    public async Task<List<CompteSelectDto>> GetPlanComptableSelectAsync(string? classe = null)
    {
        await SetAuthHeaderAsync();
        var url = "api/PlanComptable/select";
        if (!string.IsNullOrEmpty(classe)) url += $"?classe={classe}";

        return await _http.GetFromJsonAsync<List<CompteSelectDto>>(url) ?? new();
    }

    // ─── Reporting ────────────────────────────────────────────────────────────
    public async Task<BalanceWrapper?> GetBalanceAsync(Guid exerciceId, string? classe = null)
    {
        try
        {
            await SetAuthHeaderAsync();
            var url = $"api/Reporting/balance?exerciceId={exerciceId}";
            if (!string.IsNullOrEmpty(classe)) url += $"&classeCompte={classe}";

            return await _http.GetFromJsonAsync<BalanceWrapper>(url);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> CloturerExerciceAsync(Guid exerciceId)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PostAsync($"api/exercices/{exerciceId}/cloturer", null);

            if (response.IsSuccessStatusCode) return true;

            var errorResult = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            throw new Exception(errorResult != null && errorResult.ContainsKey("message")
                ? errorResult["message"]
                : "Une erreur est survenue lors de la clôture de l'exercice.");
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<GrandLivreDto?> GetGrandLivreAsync(Guid exerciceId, string? compteNumero = null, int page = 1)
    {
        await SetAuthHeaderAsync();
        var url = $"api/reporting/grand-livre?exerciceId={exerciceId}&page={page}&pageSize=20";
        if (!string.IsNullOrEmpty(compteNumero))
            url += $"&compteNumero={Uri.EscapeDataString(compteNumero)}";
        return await _http.GetFromJsonAsync<GrandLivreDto>(url);
    }

    // ─── Écritures Comptables ──────────────────────────────────────────────────
    public async Task<PagedResult<EcritureDto>?> GetEcrituresAsync(FiltreEcritureQuery filtre)
    {
        await SetAuthHeaderAsync();
        var queryString = $"?page={filtre.Page}&pageSize={filtre.PageSize}" +
                          (!string.IsNullOrEmpty(filtre.Journal) ? $"&journal={filtre.Journal}" : "") +
                          (!string.IsNullOrEmpty(filtre.Statut) ? $"&statut={filtre.Statut}" : "");

        return await _http.GetFromJsonAsync<PagedResult<EcritureDto>>($"api/Ecritures{queryString}");
    }

    public async Task<EcritureDto?> CreerEcritureAsync(CreerEcritureRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync("api/Ecritures", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<EcritureDto>() : null;
    }

    public async Task<EcritureDto?> GetEcritureAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<EcritureDto>($"api/Ecritures/{id}");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<EcritureDto?> ModifierEcritureAsync(Guid id, CreerEcritureRequest request)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PutAsJsonAsync($"api/Ecritures/{id}", request);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<EcritureDto>();

            var errorResult = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            throw new Exception(errorResult != null && errorResult.ContainsKey("message")
                ? errorResult["message"]
                : "Une erreur est survenue lors de la modification de l'écriture.");
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<bool> ValiderEcritureAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PutAsync($"api/Ecritures/{id}/valider", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SupprimerEcritureAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.DeleteAsync($"api/Ecritures/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ─── Tiers ────────────────────────────────────────────────────────────────
    public async Task<PagedResultDto<TiersDto>?> GetTiersAsync(FiltreTiersQuery filtre)
    {
        await SetAuthHeaderAsync();
        var url = $"api/tiers?page={filtre.Page}&pageSize={filtre.PageSize}";
        if (!string.IsNullOrEmpty(filtre.Type)) url += $"&type={filtre.Type}";
        if (!string.IsNullOrEmpty(filtre.Recherche)) url += $"&recherche={Uri.EscapeDataString(filtre.Recherche)}";
        if (filtre.EstActif.HasValue) url += $"&estActif={filtre.EstActif.Value.ToString().ToLower()}";
        return await _http.GetFromJsonAsync<PagedResultDto<TiersDto>>(url);
    }

    public async Task<List<TiersDto>> GetTiersSelectAsync(string? type = null)
    {
        await SetAuthHeaderAsync();
        var url = "api/tiers/recherche?pageSize=100";
        if (!string.IsNullOrEmpty(type)) url += $"&type={type}";
        return await _http.GetFromJsonAsync<List<TiersDto>>(url) ?? [];
    }

    public async Task<TiersDto?> CreerTiersAsync(CreerTiersRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync("api/Tiers", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TiersDto>() : null;
    }

    public async Task<bool> ModifierTiersAsync(Guid id, ModifierTiersRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PutAsJsonAsync($"api/Tiers/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SupprimerTiersAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        var response = await _http.DeleteAsync($"api/Tiers/{id}");
        return response.IsSuccessStatusCode;
    }

    // ─── Classes Comptables ─────────────────────────────────────────────────────
    public async Task<List<ClasseComptableModel>> GetClassesComptablesAsync()
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<List<ClasseComptableModel>>("api/ClassesComptables") ?? new();
    }

    public async Task<bool> CreerClasseComptableAsync(ClasseComptableModel model)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync("api/ClassesComptables", model);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ModifierClasseComptableAsync(int numero, string nouveauLibelle)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PutAsJsonAsync($"api/ClassesComptables/{numero}", nouveauLibelle);
        return response.IsSuccessStatusCode;
    }

    // ─── Utilisateurs & Collaborateurs ──────────────────────────────────────────
    public async Task<List<UtilisateurDto>?> GetUtilisateursTenantAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.GetFromJsonAsync<List<UtilisateurDto>>("api/Utilisateurs");
            return response ?? [];
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> CreerCollaborateurAsync(CreerCollaborateurRequest model)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PostAsJsonAsync("api/Utilisateurs", model);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task ModifierStatutUtilisateurAsync(Guid id, bool actif)
    {
        await SetAuthHeaderAsync();
        await _http.PutAsJsonAsync($"api/Utilisateurs/{id}/statut", actif);
    }

    // ─── Entreprise Profil ─────────────────────────────────────────────────────
    public async Task<EntrepriseModel?> GetEntrepriseDetailsAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<EntrepriseModel>("api/Entreprise/profil");
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ─── Devises ───────────────────────────────────────────────────────────────
    public async Task<List<DeviseDto>> GetDevisesActivesAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<DeviseDto>>("api/devises") ?? new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    public async Task<string?> GetTokenAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", "finama_token");
    public async Task<string?> GetUserNomAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", "finama_user");
    public async Task<string?> GetEntrepriseNomAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", "finama_entreprise");
    public async Task<string?> GetDeviseSymboleAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", "finama_devise");

    public async Task<bool> EstConnecteAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    private async Task SetAuthHeaderAsync()
    {
        if (!_http.DefaultRequestHeaders.Contains("ngrok-skip-browser-warning"))
        {
            _http.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
        }

        var token = await GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task SauvegarderTokenAsync(string token, string nom, string entreprise, string devise)
    {
        await _js.InvokeVoidAsync("localStorage.setItem", "finama_token", token);
        await _js.InvokeVoidAsync("localStorage.setItem", "finama_user", nom);
        await _js.InvokeVoidAsync("localStorage.setItem", "finama_entreprise", entreprise);
        await _js.InvokeVoidAsync("localStorage.setItem", "finama_devise", devise);
    }

    public async Task<bool> RegisterAsync(RegisterModel model)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PostAsJsonAsync("api/auth/register", model);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur d'inscription : {ex.Message}");
            return false;
        }
    }

    public async Task<EcritureDetailDto?> GetEcritureDetailsAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.GetAsync($"api/Ecritures/{id}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<EcritureDetailDto>();
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<PagedResult<TiersDto>> ListerTiersAsync(FiltreTiersQuery filtre)
    {
        try
        {
            await SetAuthHeaderAsync();
            var url = $"api/Tiers?recherche={filtre.Recherche}&type={filtre.Type}&page={filtre.Page}&pageSize={filtre.PageSize}";
            return await _http.GetFromJsonAsync<PagedResult<TiersDto>>(url) ?? new PagedResult<TiersDto>();
        }
        catch (Exception)
        {
            return new PagedResult<TiersDto>();
        }
    }

    public async Task<List<PaysDto>> GetPaysAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<PaysDto>>("api/Pays") ?? new();
        }
        catch { return new(); }
    }
}