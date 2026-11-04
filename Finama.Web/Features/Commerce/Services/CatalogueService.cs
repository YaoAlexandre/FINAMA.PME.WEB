using Microsoft.AspNetCore.Components;

namespace Finama.Web.Features.Commerce.Services;

public interface ICatalogueService
{
    Task<List<ProduitCommerceDto>> GetProduitsAsync(Guid? magasinId = null);
    Task<bool> CreerProduitAsync(ProduitCommerceDto produit);
    Task<bool> ModifierProduitAsync(Guid id, ProduitCommerceDto produit);
    Task<bool> DesactiverProduitAsync(Guid id);
    Task<List<CategorieCommerceDto>> GetCategoriesAsync();
    Task<CategorieCommerceDto?> CreerCategorieAsync(SaveCategorieCommerceRequest request);
    Task<CategorieCommerceDto?> ModifierCategorieAsync(Guid id, SaveCategorieCommerceRequest request);
    Task<bool> SupprimerCategorieAsync(Guid id);
    Task<List<EditeurCommerceDto>> GetEditeursAsync();
    Task<EditeurCommerceDto?> CreerEditeurAsync(SaveEditeurCommerceRequest request);
    Task<EditeurCommerceDto?> ModifierEditeurAsync(Guid id, SaveEditeurCommerceRequest request);
    Task<bool> DefinirEditeurActifAsync(Guid id, bool actif);
}

public sealed class CatalogueService : CommerceServiceBase, ICatalogueService
{
    public CatalogueService(HttpClient http, ILocalStorage stockage, NavigationManager nav, ErreursCommerce erreurs)
        : base(http, stockage, nav, erreurs) { }

    // ─── CATALOGUE ───────────────────────────────────────────────────────────

    public async Task<List<ProduitCommerceDto>> GetProduitsAsync(Guid? magasinId = null)
    {
        magasinId ??= await MagasinActifIdAsync();
        var url = magasinId.HasValue ? $"api/commerce/produits?magasinId={magasinId}" : "api/commerce/produits";
        return await ListeAsync<ProduitCommerceDto>(url);
    }

    public async Task<bool> CreerProduitAsync(ProduitCommerceDto produit)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/produits", produit));

    public async Task<bool> ModifierProduitAsync(Guid id, ProduitCommerceDto produit)
        => Ok(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/produits/{id}", produit));

    public async Task<bool> DesactiverProduitAsync(Guid id)
        => Ok(await EnvoyerAsync(HttpMethod.Delete, $"api/commerce/produits/{id}"));

    // ─── CATÉGORIES & ÉDITEURS ───────────────────────────────────────────────

    public async Task<List<CategorieCommerceDto>> GetCategoriesAsync()
        => await ListeAsync<CategorieCommerceDto>("api/commerce/categories");

    public async Task<CategorieCommerceDto?> CreerCategorieAsync(SaveCategorieCommerceRequest request)
        => await LireAsync<CategorieCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/categories", request));

    public async Task<CategorieCommerceDto?> ModifierCategorieAsync(Guid id, SaveCategorieCommerceRequest request)
        => await LireAsync<CategorieCommerceDto>(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/categories/{id}", request));

    public async Task<bool> SupprimerCategorieAsync(Guid id)
        => Ok(await EnvoyerAsync(HttpMethod.Delete, $"api/commerce/categories/{id}"));

    public async Task<List<EditeurCommerceDto>> GetEditeursAsync()
        => await ListeAsync<EditeurCommerceDto>("api/commerce/editeurs");

    public async Task<EditeurCommerceDto?> CreerEditeurAsync(SaveEditeurCommerceRequest request)
        => await LireAsync<EditeurCommerceDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/editeurs", request));

    public async Task<EditeurCommerceDto?> ModifierEditeurAsync(Guid id, SaveEditeurCommerceRequest request)
        => await LireAsync<EditeurCommerceDto>(await EnvoyerAsync(HttpMethod.Put, $"api/commerce/editeurs/{id}", request));

    public async Task<bool> DefinirEditeurActifAsync(Guid id, bool actif)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/editeurs/{id}/actif?actif={actif.ToString().ToLower()}"));

}
