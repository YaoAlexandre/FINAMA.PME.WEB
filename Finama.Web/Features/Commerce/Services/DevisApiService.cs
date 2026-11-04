using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Finama.Web.Features.Commerce.Services;

public class DevisApiService
{
    private readonly HttpClient _http;
    private readonly ILocalStorage _stockage;
    private readonly NavigationManager _nav;

    public DevisApiService(HttpClient http, ILocalStorage stockage, NavigationManager nav)
    {
        _http = http;
        _stockage = stockage;
        _nav = nav;
    }

    // ─── Devis ────────────────────────────────────────────────────────────────

    public async Task<List<DevisDto>> GetMesDevisAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<DevisDto>>("api/devis") ?? new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public async Task<DevisDto?> GetDevisAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<DevisDto>($"api/devis/{id}");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<DevisDto?> CreerDevisAsync(CreerDevisRequest request)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PostAsJsonAsync("api/devis", request);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<DevisDto>()
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<DevisDto?> ModifierDevisAsync(Guid id, CreerDevisRequest request)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PutAsJsonAsync($"api/devis/{id}", request);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<DevisDto>();

            var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            throw new Exception(error != null && error.ContainsKey("message")
                ? error["message"]
                : "Erreur lors de la modification du devis.");
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<bool> MettreAJourStatutDevisAsync(Guid id, StatutDevis statut)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PatchAsJsonAsync(
                $"api/devis/{id}/statut",
                new { Statut = (int)statut });
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<Guid?> ConvertirEnFactureAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PostAsync($"api/devis/{id}/convertir", null);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                throw new Exception(error != null && error.ContainsKey("message")
                    ? error["message"]
                    : "Erreur lors de la conversion en facture.");
            }

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
            return result != null && result.ContainsKey("factureId")
                ? result["factureId"]
                : null;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<bool> SupprimerDevisAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.DeleteAsync($"api/devis/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    // Jeton + X-Device-Id : ajoutés par AuthHeaderHandler (une seule source de vérité, refresh automatique).
    private async Task SetAuthHeaderAsync() => await GetDeviceIdAsync();

    private async Task<string> GetDeviceIdAsync()
    {
        var id = await _stockage.LireAsync(ClesStockage.Appareil);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            await _stockage.EcrireAsync(ClesStockage.Appareil, id);
        }
        return id;
    }

    // Ajouter après SupprimerDevisAsync

    public async Task<Guid?> ConvertirDevisEnFactureAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PostAsync($"api/devis/{id}/convertir", null);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                throw new Exception(error != null && error.ContainsKey("message")
                    ? error["message"]
                    : "Erreur lors de la conversion en facture.");
            }

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
            return result != null && result.ContainsKey("factureId")
                ? result["factureId"]
                : null;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<List<TiersDto>> GetTiersSelectAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<TiersDto>>("api/tiers/recherche?pageSize=100") ?? new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    // ─── Commercial : Clients ───────────────────────────────────────────────────

    public async Task<List<ClientCommercialDto>> GetClientsCommercialAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<ClientCommercialDto>>("api/commercial/clients") ?? new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    // ─── Commercial : Pipeline Kanban ───────────────────────────────────────────

    public async Task<List<OpportuniteDto>> GetPipelineOpportunitesAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<OpportuniteDto>>("api/commercial/pipeline") ?? new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public async Task<bool> DeplacerOpportuniteAsync(Guid id, EtapePipeline nouvelleEtape)
    {
        try
        {
            await SetAuthHeaderAsync();
            var request = new ChangerEtapeRequest { NouvelleEtape = nouvelleEtape };
            var response = await _http.PostAsJsonAsync($"api/commercial/pipeline/{id}/changer-etape", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}