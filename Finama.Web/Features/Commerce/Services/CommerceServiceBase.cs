using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;

namespace Finama.Web.Features.Commerce.Services;

/// <summary>Dernière erreur des appels Commerce, partagée par tous les services du module (portée : la session).</summary>
public sealed class ErreursCommerce
{
    /// <summary>Message de la dernière erreur (API, réseau, session). Null si le dernier appel a réussi.</summary>
    public string? Derniere { get; internal set; }
}

/// <summary>
/// Plomberie HTTP commune aux services du module Commerce : envoi, lecture du message d'erreur,
/// redirection si la session est perdue, injection du magasin actif.
/// </summary>
public abstract class CommerceServiceBase
{
    private readonly HttpClient _http;
    private readonly ILocalStorage _stockage;
    private readonly NavigationManager _nav;
    private readonly ErreursCommerce _erreurs;

    protected CommerceServiceBase(HttpClient http, ILocalStorage stockage, NavigationManager nav, ErreursCommerce erreurs)
    {
        _http = http;
        _stockage = stockage;
        _nav = nav;
        _erreurs = erreurs;
    }

    protected string? DerniereErreur
    {
        get => _erreurs.Derniere;
        set => _erreurs.Derniere = value;
    }

    protected async Task<Guid?> MagasinActifIdAsync()
    {
        var valeur = await _stockage.LireAsync(ClesStockage.MagasinId);
        return Guid.TryParse(valeur, out var id) && id != Guid.Empty ? id : null;
    }

    protected async Task<HttpResponseMessage?> EnvoyerAsync(HttpMethod methode, string url, object? corps = null)
    {
        DerniereErreur = null;
        try
        {
            using var requete = new HttpRequestMessage(methode, url);
            if (corps != null) requete.Content = JsonContent.Create(corps, corps.GetType());

            var reponse = await _http.SendAsync(requete);

            if (reponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                DerniereErreur = "Session expirée. Veuillez vous reconnecter.";
                _nav.NavigateTo("/connexion", forceLoad: true);
                return null;
            }

            if (!reponse.IsSuccessStatusCode)
                DerniereErreur = await LireErreurAsync(reponse);

            return reponse;
        }
        catch (Exception)
        {
            DerniereErreur = MessageReseau;
            return null;
        }
    }

    protected const string MessageReseau = "Impossible de joindre le serveur. Vérifiez votre connexion puis réessayez.";
    protected const string MessageServeur = "Une erreur est survenue sur le serveur. Réessayez dans un instant ; si le problème persiste, contactez le support.";

    protected static async Task<string> LireErreurAsync(HttpResponseMessage reponse)
    {
        // Erreur serveur (5xx) : le corps peut contenir des détails techniques, on ne l'affiche jamais.
        if ((int)reponse.StatusCode >= 500) return MessageServeur;
        if (reponse.StatusCode == HttpStatusCode.Forbidden) return "Vous n'avez pas l'autorisation d'effectuer cette action.";

        var texte = await reponse.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(texte)) return $"Erreur {(int)reponse.StatusCode}.";

        try
        {
            using var doc = JsonDocument.Parse(texte);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var nom in new[] { "message", "Message", "detail", "title" })
                {
                    if (doc.RootElement.TryGetProperty(nom, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString() ?? texte;
                }
            }
        }
        catch { /* texte brut */ }

        // Texte brut non JSON (ex. page d'erreur HTML) : message générique plutôt que le contenu.
        return texte.TrimStart().StartsWith('<') || texte.Length > 300 ? $"La demande n'a pas abouti (erreur {(int)reponse.StatusCode})." : texte;
    }

    protected static async Task<T?> LireAsync<T>(HttpResponseMessage? reponse)
    {
        if (reponse is not { IsSuccessStatusCode: true } || reponse.StatusCode == HttpStatusCode.NoContent) return default;
        return await reponse.Content.ReadFromJsonAsync<T>();
    }

    protected static bool Ok(HttpResponseMessage? reponse) => reponse is { IsSuccessStatusCode: true };

    protected async Task<List<T>> ListeAsync<T>(string url)
        => await LireAsync<List<T>>(await EnvoyerAsync(HttpMethod.Get, url)) ?? new();

    /// <summary>Injecte le magasin actif de la session si la requête n'en précise pas (sinon le serveur prend le principal).</summary>
    protected async Task<JsonObject> AvecMagasinAsync(object requete)
    {
        var noeud = JsonSerializer.SerializeToNode(requete, requete.GetType(), JsonSerializerOptions.Web) as JsonObject ?? new JsonObject();
        var actuel = noeud["magasinId"]?.ToString();
        if (!Guid.TryParse(actuel, out var g) || g == Guid.Empty)
        {
            var actif = await MagasinActifIdAsync();
            if (actif.HasValue) noeud["magasinId"] = actif.Value.ToString();
        }
        return noeud;
    }
}
