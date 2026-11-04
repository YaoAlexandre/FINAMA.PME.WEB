using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Finama.Web.Services;

/// <summary>
/// Photos des articles : liste chargée une fois par session (un seul appel), envoi et suppression.
/// L'image est réduite dans le navigateur avant l'envoi (600 px, JPEG) : rapide même en 3G.
/// </summary>
public class ImagesArticlesService
{
    private const string CleCache = ClesStockage.ImagesArticles;
    private readonly HttpClient _http;
    private readonly ILocalStorage _stockage;
    private Dictionary<Guid, string>? _urls;
    private Task? _chargement;

    public ImagesArticlesService(HttpClient http, ILocalStorage stockage)
    {
        _http = http;
        _stockage = stockage;
    }

    public sealed record ImageArticle(Guid ProduitId, string Url);

    public Task ChargerAsync()
    {
        if (_urls is not null) return Task.CompletedTask;
        return _chargement ??= ChargerInterneAsync();
    }

    private async Task ChargerInterneAsync()
    {
        try
        {
            var liste = await _http.GetFromJsonAsync<List<ImageArticle>>("api/commerce/produits/images") ?? new();
            _urls = liste.ToDictionary(i => i.ProduitId, i => i.Url);
            await MemoriserAsync();
        }
        catch
        {
            // Hors connexion : dernière liste connue (les photos elles-mêmes sont gardées par le service worker)
            try
            {
                var brut = await _stockage.LireAsync(CleCache);
                _urls = string.IsNullOrEmpty(brut) ? new() : JsonSerializer.Deserialize<Dictionary<Guid, string>>(brut) ?? new();
            }
            catch { _urls = new(); }
        }
        finally { _chargement = null; }
    }

    public string? Url(Guid produitId) => _urls?.GetValueOrDefault(produitId);

    public void Invalider() => _urls = null;

    /// <summary>Réduit la photo (600 × 600 maximum, JPEG) puis l'envoie. Renvoie un message d'erreur, ou null si tout va bien.</summary>
    public async Task<string?> EnvoyerAsync(Guid produitId, IBrowserFile fichier)
    {
        if (!fichier.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return "Choisissez une image (photo JPEG, PNG ou WebP).";
        try
        {
            var reduite = await fichier.RequestImageFileAsync("image/jpeg", 600, 600);
            await using var flux = reduite.OpenReadStream(maxAllowedSize: 1_500_000);
            using var contenu = new StreamContent(flux);
            contenu.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            using var formulaire = new MultipartFormDataContent { { contenu, "fichier", "photo.jpg" } };

            using var r = await _http.PutAsync($"api/commerce/produits/{produitId}/image", formulaire);
            if (!r.IsSuccessStatusCode)
            {
                try { return (await r.Content.ReadFromJsonAsync<Reponse>())?.Message ?? "Envoi refusé."; } catch { return "Envoi refusé."; }
            }
            var image = await r.Content.ReadFromJsonAsync<ImageArticle>();
            if (image is not null) { (_urls ??= new())[produitId] = image.Url; await MemoriserAsync(); }
            return null;
        }
        catch (IOException) { return "Image trop lourde, même réduite : essayez une autre photo."; }
        catch { return "Envoi impossible : vérifiez votre connexion."; }
    }

    public async Task<bool> SupprimerAsync(Guid produitId)
    {
        try
        {
            using var r = await _http.DeleteAsync($"api/commerce/produits/{produitId}/image");
            if (r.IsSuccessStatusCode) { _urls?.Remove(produitId); await MemoriserAsync(); }
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private async Task MemoriserAsync()
    {
        await _stockage.EcrireAsync(CleCache, JsonSerializer.Serialize(_urls ?? new()));
    }

    private sealed record Reponse(string? Message);
}
