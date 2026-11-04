using System.Net.Http.Json;
using Finama.Web.Core.Securite;
using Finama.Web.Models;

namespace Finama.Web.Features.Comptabilite.Services;

public interface ITiersService
{
    Task<PagedResultDto<TiersDto>?> GetTiersAsync(FiltreTiersQuery filtre);
    Task<List<TiersDto>> GetTiersSelectAsync(string? type = null);
    Task<TiersDto?> CreerTiersAsync(CreerTiersRequest request);
    Task<bool> ModifierTiersAsync(Guid id, ModifierTiersRequest request);
    Task<bool> SupprimerTiersAsync(Guid id);
    Task<PagedResult<TiersDto>> ListerTiersAsync(FiltreTiersQuery filtre);
}

public sealed class TiersService : ITiersService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public TiersService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    // ─── Tiers ────────────────────────────────────────────────────────────────
    public async Task<PagedResultDto<TiersDto>?> GetTiersAsync(FiltreTiersQuery filtre)
    {
        await _appareil.GarantirAsync();
        var url = $"api/tiers?page={filtre.Page}&pageSize={filtre.PageSize}";
        if (!string.IsNullOrEmpty(filtre.Type)) url += $"&type={filtre.Type}";
        if (!string.IsNullOrEmpty(filtre.Recherche)) url += $"&recherche={Uri.EscapeDataString(filtre.Recherche)}";
        if (filtre.EstActif.HasValue) url += $"&estActif={filtre.EstActif.Value.ToString().ToLower()}";
        return await _http.GetFromJsonAsync<PagedResultDto<TiersDto>>(url);
    }

    public async Task<List<TiersDto>> GetTiersSelectAsync(string? type = null)
    {
        await _appareil.GarantirAsync();
        var url = "api/tiers/recherche?pageSize=100";
        if (!string.IsNullOrEmpty(type)) url += $"&type={type}";
        return await _http.GetFromJsonAsync<List<TiersDto>>(url) ?? [];
    }

    public async Task<TiersDto?> CreerTiersAsync(CreerTiersRequest request)
    {
        await _appareil.GarantirAsync();
        var response = await _http.PostAsJsonAsync("api/Tiers", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TiersDto>() : null;
    }

    public async Task<bool> ModifierTiersAsync(Guid id, ModifierTiersRequest request)
    {
        await _appareil.GarantirAsync();
        var response = await _http.PutAsJsonAsync($"api/Tiers/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SupprimerTiersAsync(Guid id)
    {
        await _appareil.GarantirAsync();
        var response = await _http.DeleteAsync($"api/Tiers/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<PagedResult<TiersDto>> ListerTiersAsync(FiltreTiersQuery filtre)
    {
        try
        {
            await _appareil.GarantirAsync();
            var url = $"api/Tiers?recherche={filtre.Recherche}&type={filtre.Type}&page={filtre.Page}&pageSize={filtre.PageSize}";
            return await _http.GetFromJsonAsync<PagedResult<TiersDto>>(url) ?? new PagedResult<TiersDto>();
        }
        catch (Exception)
        {
            return new PagedResult<TiersDto>();
        }
    }

}
