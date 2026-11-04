using Microsoft.AspNetCore.Components;

namespace Finama.Web.Features.Commerce.Services;

public interface IStocksService
{
    Task<List<StockLotDto>> GetStocksLotsAsync();
    Task<(bool Succes, string MessageErreur)> EnregistrerEntreeInitialeAsync(object request);
    Task<bool> AjusterStockAsync(object request);
    Task<List<InventaireDto>> GetInventairesAsync();
    Task<InventaireDto?> OuvrirInventaireAsync(OuvrirInventaireRequest request);
    Task<InventaireDto?> GetInventaireDetailsAsync(Guid id);
    Task<bool> CompterInventaireAsync(Guid inventaireId, object request);
    Task<bool> CloturerInventaireAsync(Guid inventaireId, object request);
    Task<bool> AnnulerInventaireAsync(Guid inventaireId);
}

public sealed class StocksService : CommerceServiceBase, IStocksService
{
    public StocksService(HttpClient http, ILocalStorage stockage, NavigationManager nav, ErreursCommerce erreurs)
        : base(http, stockage, nav, erreurs) { }

    // ─── STOCKS & LOTS ───────────────────────────────────────────────────────

    public async Task<List<StockLotDto>> GetStocksLotsAsync()
    {
        var magasinId = await MagasinActifIdAsync();
        var url = magasinId.HasValue ? $"api/commerce/stock/lots?magasinId={magasinId}" : "api/commerce/stock/lots";
        return await ListeAsync<StockLotDto>(url);
    }

    public async Task<(bool Succes, string MessageErreur)> EnregistrerEntreeInitialeAsync(object request)
    {
        var reponse = await EnvoyerAsync(HttpMethod.Post, "api/commerce/stock/entree-initiale", await AvecMagasinAsync(request));
        return Ok(reponse) ? (true, string.Empty) : (false, DerniereErreur ?? "Erreur inconnue du serveur.");
    }

    public async Task<bool> AjusterStockAsync(object request)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/stock/ajuster", await AvecMagasinAsync(request)));

    // ─── INVENTAIRES ─────────────────────────────────────────────────────────

    public async Task<List<InventaireDto>> GetInventairesAsync()
        => await ListeAsync<InventaireDto>("api/commerce/stock/inventaires");

    public async Task<InventaireDto?> OuvrirInventaireAsync(OuvrirInventaireRequest request)
    {
        if (request.MagasinId == Guid.Empty)
            request.MagasinId = await MagasinActifIdAsync() ?? Guid.Empty;
        return await LireAsync<InventaireDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/stock/inventaires/ouvrir", request));
    }

    public async Task<InventaireDto?> GetInventaireDetailsAsync(Guid id)
        => await LireAsync<InventaireDto>(await EnvoyerAsync(HttpMethod.Get, $"api/commerce/stock/inventaires/{id}"));

    public async Task<bool> CompterInventaireAsync(Guid inventaireId, object request)
    => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/stock/inventaires/{inventaireId}/compter", request));
    public async Task<bool> CloturerInventaireAsync(Guid inventaireId, object request)
    => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/stock/inventaires/{inventaireId}/cloturer", request));

    public async Task<bool> AnnulerInventaireAsync(Guid inventaireId)
        => Ok(await EnvoyerAsync(HttpMethod.Post, $"api/commerce/stock/inventaires/{inventaireId}/annuler"));

}
