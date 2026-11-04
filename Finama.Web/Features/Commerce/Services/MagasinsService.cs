using Microsoft.AspNetCore.Components;

namespace Finama.Web.Features.Commerce.Services;

public interface IMagasinsService
{
    Task<List<MagasinDto>> GetMagasinsAsync();
    Task<bool> CreerMagasinAsync(CreerMagasinRequest request);
    Task<bool> DefinirMagasinPrincipalAsync(Guid id);
    Task<Guid?> GetMagasinActifIdAsync();
}

public sealed class MagasinsService : CommerceServiceBase, IMagasinsService
{
    public MagasinsService(HttpClient http, ILocalStorage stockage, NavigationManager nav, ErreursCommerce erreurs)
        : base(http, stockage, nav, erreurs) { }

    // ─── MAGASINS ────────────────────────────────────────────────────────────

    public async Task<List<MagasinDto>> GetMagasinsAsync()
        => await ListeAsync<MagasinDto>("api/commerce/magasins");

    public async Task<bool> CreerMagasinAsync(CreerMagasinRequest request)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/magasins", request));

    public async Task<bool> DefinirMagasinPrincipalAsync(Guid id)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/magasins/{id}/principal"));

    /// <summary>Magasin choisi pour la session (localStorage), ou null : le serveur prend alors le principal.</summary>
    public Task<Guid?> GetMagasinActifIdAsync() => MagasinActifIdAsync();
}
