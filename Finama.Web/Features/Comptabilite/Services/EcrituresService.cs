using System.Net.Http.Json;
using Finama.Web.Core.Securite;

namespace Finama.Web.Features.Comptabilite.Services;

public interface IEcrituresService
{
    Task<PagedResult<EcritureDto>?> GetEcrituresAsync(FiltreEcritureQuery filtre);
    Task<EcritureDto?> CreerEcritureAsync(CreerEcritureRequest request);
    Task<EcritureDto?> GetEcritureAsync(Guid id);
    Task<EcritureDto?> ModifierEcritureAsync(Guid id, CreerEcritureRequest request);
    Task<bool> ValiderEcritureAsync(Guid id);
    Task<bool> SupprimerEcritureAsync(Guid id);
    Task<EcritureDetailDto?> GetEcritureDetailsAsync(Guid id);
}

public sealed class EcrituresService : IEcrituresService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public EcrituresService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    // ─── Écritures Comptables ──────────────────────────────────────────────────
    public async Task<PagedResult<EcritureDto>?> GetEcrituresAsync(FiltreEcritureQuery filtre)
    {
        await _appareil.GarantirAsync();
        var queryString = $"?page={filtre.Page}&pageSize={filtre.PageSize}" +
                          (!string.IsNullOrEmpty(filtre.Journal) ? $"&journal={filtre.Journal}" : "") +
                          (!string.IsNullOrEmpty(filtre.Statut) ? $"&statut={filtre.Statut}" : "");

        return await _http.GetFromJsonAsync<PagedResult<EcritureDto>>($"api/Ecritures{queryString}");
    }

    public async Task<EcritureDto?> CreerEcritureAsync(CreerEcritureRequest request)
    {
        await _appareil.GarantirAsync();
        var response = await _http.PostAsJsonAsync("api/Ecritures", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<EcritureDto>() : null;
    }

    public async Task<EcritureDto?> GetEcritureAsync(Guid id)
    {
        try
        {
            await _appareil.GarantirAsync();
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
            await _appareil.GarantirAsync();
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
        await _appareil.GarantirAsync();
        var response = await _http.PutAsync($"api/Ecritures/{id}/valider", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SupprimerEcritureAsync(Guid id)
    {
        try
        {
            await _appareil.GarantirAsync();
            var response = await _http.DeleteAsync($"api/Ecritures/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<EcritureDetailDto?> GetEcritureDetailsAsync(Guid id)
    {
        try
        {
            await _appareil.GarantirAsync();
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

}
