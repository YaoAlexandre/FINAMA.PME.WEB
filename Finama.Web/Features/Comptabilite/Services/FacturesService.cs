using System.Net.Http.Json;
using Finama.Web.Core.Securite;

namespace Finama.Web.Features.Comptabilite.Services;

public interface IFacturesService
{
    Task<PagedResultDto<FactureDto>?> GetFacturesAsync(int page = 1, int pageSize = 20);
    Task<FactureDto?> GetFactureAsync(Guid id);
    Task<FactureDto?> CreerFactureAsync(CreerFactureRequest request);
    Task<byte[]?> GetFacturePdfAsync(Guid id);
}

public sealed class FacturesService : IFacturesService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public FacturesService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    // ─── Factures ─────────────────────────────────────────────────────────────
    public async Task<PagedResultDto<FactureDto>?> GetFacturesAsync(int page = 1, int pageSize = 20)
    {
        await _appareil.GarantirAsync();
        return await _http.GetFromJsonAsync<PagedResultDto<FactureDto>>(
            $"api/factures?page={page}&pageSize={pageSize}");
    }

    public async Task<FactureDto?> GetFactureAsync(Guid id)
    {
        await _appareil.GarantirAsync();
        return await _http.GetFromJsonAsync<FactureDto>($"api/factures/{id}");
    }

    public async Task<FactureDto?> CreerFactureAsync(CreerFactureRequest request)
    {
        await _appareil.GarantirAsync();
        var r = await _http.PostAsJsonAsync("api/factures", request);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await ApiClient.LireMessageErreurAsync(r, "La facture n'a pas pu être créée."));
        return await r.Content.ReadFromJsonAsync<FactureDto>();
    }

    public async Task<byte[]?> GetFacturePdfAsync(Guid id)
    {
        await _appareil.GarantirAsync();
        var r = await _http.GetAsync($"api/factures/{id}/pdf");
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadAsByteArrayAsync();
    }

}
