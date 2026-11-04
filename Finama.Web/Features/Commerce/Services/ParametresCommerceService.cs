using Microsoft.AspNetCore.Components;

namespace Finama.Web.Features.Commerce.Services;

public interface IParametresCommerceService
{
    Task<TableauBordCommerceDto?> GetTableauBordAsync();
    Task<ParametresCommerceDto?> GetParametresAsync();
    Task<ParametresCommerceDto?> SauvegarderParametresAsync(ParametresCommerceDto parametres);
    Task<List<ProfilActiviteDto>> GetProfilsActiviteAsync();
    Task<ParametresCommerceDto?> AppliquerProfilAsync(string code);
}

public sealed class ParametresCommerceService : CommerceServiceBase, IParametresCommerceService
{
    public ParametresCommerceService(HttpClient http, ILocalStorage stockage, NavigationManager nav, ErreursCommerce erreurs)
        : base(http, stockage, nav, erreurs) { }

    // ─── TABLEAU DE BORD ─────────────────────────────────────────────────────

    public async Task<TableauBordCommerceDto?> GetTableauBordAsync()
        => await LireAsync<TableauBordCommerceDto>(await EnvoyerAsync(HttpMethod.Get, "api/commerce/tableau-bord"));

    // ─── PARAMÈTRES & PROFILS D'ACTIVITÉ ─────────────────────────────────────

    public async Task<ParametresCommerceDto?> GetParametresAsync()
        => await LireAsync<ParametresCommerceDto>(await EnvoyerAsync(HttpMethod.Get, "api/commerce/parametres"));

    public async Task<ParametresCommerceDto?> SauvegarderParametresAsync(ParametresCommerceDto parametres)
        => await LireAsync<ParametresCommerceDto>(await EnvoyerAsync(HttpMethod.Put, "api/commerce/parametres", parametres));

    public async Task<List<ProfilActiviteDto>> GetProfilsActiviteAsync()
        => await ListeAsync<ProfilActiviteDto>("api/commerce/parametres/profils");

    public async Task<ParametresCommerceDto?> AppliquerProfilAsync(string code)
        => await LireAsync<ParametresCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/parametres/appliquer-profil", new { code }));

}
