using System.Net.Http.Json;
using Finama.Web.Core.Securite;
using Finama.Web.Models;

namespace Finama.Web.Features.Administration.Services;

public interface IEntrepriseService
{
    Task<EntrepriseModel?> GetEntrepriseDetailsAsync();
    Task UpdateEntrepriseAsync(EntrepriseModel entreprise);
    Task<List<DeviseDto>> GetDevisesActivesAsync();
    Task<List<PaysDto>> GetPaysAsync();
}

public sealed class EntrepriseService : IEntrepriseService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public EntrepriseService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    // ─── Entreprise Profil ─────────────────────────────────────────────────────
    public async Task<EntrepriseModel?> GetEntrepriseDetailsAsync()
    {
        try
        {
            await _appareil.GarantirAsync();
            var entreprise = await _http.GetFromJsonAsync<EntrepriseModel>("api/Entreprise/profil");
            return entreprise;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task UpdateEntrepriseAsync(EntrepriseModel entreprise)
    {
        await _appareil.GarantirAsync();
        var r = await _http.PutAsJsonAsync("api/entreprise/profil", new
        {
            Nom = entreprise.Nom,
            Adresse = entreprise.Adresse,
            Telephone = entreprise.Telephone,
            NumeroFiscal = entreprise.NumeroFiscal,
            BanqueNom = entreprise.BanqueNom,
            BanqueBIC = entreprise.BanqueBIC,
            TauxTVA = entreprise.TauxTVA,
        });
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await ApiClient.LireMessageErreurAsync(r, "Les informations n'ont pas pu être enregistrées."));
    }

    // ─── Devises ───────────────────────────────────────────────────────────────
    public async Task<List<DeviseDto>> GetDevisesActivesAsync()
    {
        try
        {
            await _appareil.GarantirAsync();
            return await _http.GetFromJsonAsync<List<DeviseDto>>("api/devises") ?? new();
        }
        catch (Exception)
        {
            return new();
        }
    }

    public async Task<List<PaysDto>> GetPaysAsync()
    {
        try
        {
            await _appareil.GarantirAsync();
            return await _http.GetFromJsonAsync<List<PaysDto>>("api/Pays") ?? new();
        }
        catch { return new(); }
    }

}
