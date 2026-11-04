using System.Net.Http.Json;
using Finama.Web.Core.Securite;

namespace Finama.Web.Features.Comptabilite.Services;

public interface IEtatsFinanciersService
{
    Task<BalanceWrapper?> GetBalanceAsync(Guid exerciceId, string? classe = null, DateTime? dateDebut = null, DateTime? dateFin = null);
    Task<GrandLivreDto?> GetGrandLivreAsync(Guid exerciceId, string? compteNumero = null, int page = 1);
}

public sealed class EtatsFinanciersService : IEtatsFinanciersService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public EtatsFinanciersService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    // ─── Reporting ────────────────────────────────────────────────────────────
    public async Task<BalanceWrapper?> GetBalanceAsync(Guid exerciceId, string? classe = null,
                                                   DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        try
        {
            await _appareil.GarantirAsync();
            var url = $"api/Reporting/balance?exerciceId={exerciceId}";
            if (!string.IsNullOrEmpty(classe)) url += $"&classeCompte={classe}";
            if (dateDebut is { } d) url += $"&dateDebut={d:yyyy-MM-dd}";
            if (dateFin is { } f) url += $"&dateFin={f:yyyy-MM-dd}";

            return await _http.GetFromJsonAsync<BalanceWrapper>(url);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<GrandLivreDto?> GetGrandLivreAsync(Guid exerciceId, string? compteNumero = null, int page = 1)
    {
        await _appareil.GarantirAsync();
        var url = $"api/reporting/grand-livre?exerciceId={exerciceId}&page={page}&pageSize=20";
        if (!string.IsNullOrEmpty(compteNumero))
            url += $"&compteNumero={Uri.EscapeDataString(compteNumero)}";
        return await _http.GetFromJsonAsync<GrandLivreDto>(url);
    }

}
