using Microsoft.AspNetCore.Components;

namespace Finama.Web.Features.Commerce.Services;

public interface IClientsCommerceService
{
    Task<List<ClientCommerceDto>> RechercherClientsAsync(string? recherche = null);
    Task<ClientDetailCommerceDto?> GetClientAsync(Guid id);
    Task<ClientCommerceDto?> CreerClientAsync(SaveClientCommerceRequest request);
    Task<ClientCommerceDto?> ModifierClientAsync(Guid id, SaveClientCommerceRequest request);
    Task<bool> DefinirClientActifAsync(Guid id, bool actif);
}

public sealed class ClientsCommerceService : CommerceServiceBase, IClientsCommerceService
{
    public ClientsCommerceService(HttpClient http, ILocalStorage stockage, NavigationManager nav, ErreursCommerce erreurs)
        : base(http, stockage, nav, erreurs) { }

    // ─── CLIENTS ─────────────────────────────────────────────────────────────

    public async Task<List<ClientCommerceDto>> RechercherClientsAsync(string? recherche = null)
    {
        var url = string.IsNullOrWhiteSpace(recherche)
            ? "api/commerce/clients"
            : $"api/commerce/clients?recherche={Uri.EscapeDataString(recherche)}";
        return await ListeAsync<ClientCommerceDto>(url);
    }

    public async Task<ClientDetailCommerceDto?> GetClientAsync(Guid id)
        => await LireAsync<ClientDetailCommerceDto>(await EnvoyerAsync(HttpMethod.Get, $"api/commerce/clients/{id}"));

    public async Task<ClientCommerceDto?> CreerClientAsync(SaveClientCommerceRequest request)
        => await LireAsync<ClientCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/clients", request));

    public async Task<ClientCommerceDto?> ModifierClientAsync(Guid id, SaveClientCommerceRequest request)
        => await LireAsync<ClientCommerceDto>(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/clients/{id}", request));

    public async Task<bool> DefinirClientActifAsync(Guid id, bool actif)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/clients/{id}/actif?actif={actif.ToString().ToLower()}"));

}
