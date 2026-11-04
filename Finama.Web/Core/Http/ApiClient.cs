using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finama.Web.Models;

namespace Finama.Web.Core.Http;

/// <summary>Résultat d'un appel API qui renvoie des données.</summary>
public sealed record ResultatApi<T>(bool Succes, T? Donnees, string Message, HttpStatusCode? Statut = null)
{
    public ResultatApi SansDonnees() => new(Succes, Message);
}

/// <summary>
/// Client HTTP unique des services : sérialisation JSON, lecture du message d'erreur de l'API
/// ({ "message": "…" }) et gestion du réseau injoignable, au même endroit.
/// Ne lève jamais d'exception pour une erreur HTTP ou réseau : tout passe par <see cref="ResultatApi{T}"/>.
/// L'authentification (Bearer, X-Device-Id, refresh) reste assurée par AuthHeaderHandler.
/// </summary>
public sealed class ApiClient
{
    public const string MessageReseau = "Serveur injoignable. Vérifiez votre connexion puis réessayez.";
    public const string MessageServeur = "Erreur du serveur. Réessayez dans un instant.";
    public const string MessageTropDeTentatives = "Trop de tentatives. Patientez une minute.";

    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    // ─── Avec données ─────────────────────────────────────────────────────────

    public Task<ResultatApi<T>> GetAsync<T>(string url, string erreur = "Chargement impossible.")
        => EnvoyerAsync<T>(() => new HttpRequestMessage(HttpMethod.Get, url), erreur, "");

    public Task<ResultatApi<T>> PostAsync<T>(string url, object? corps, string erreur = "Opération impossible.", string succes = "")
        => EnvoyerAsync<T>(() => Requete(HttpMethod.Post, url, corps), erreur, succes);

    public Task<ResultatApi<T>> PutAsync<T>(string url, object? corps, string erreur = "Enregistrement impossible.", string succes = "")
        => EnvoyerAsync<T>(() => Requete(HttpMethod.Put, url, corps), erreur, succes);

    /// <summary>Contenu binaire (PDF, image…).</summary>
    public async Task<ResultatApi<byte[]>> GetOctetsAsync(string url, string erreur = "Téléchargement impossible.")
    {
        try
        {
            using var reponse = await _http.GetAsync(url);
            if (!reponse.IsSuccessStatusCode)
                return new(false, null, await LireMessageErreurAsync(reponse, erreur), reponse.StatusCode);
            return new(true, await reponse.Content.ReadAsByteArrayAsync(), "", reponse.StatusCode);
        }
        catch (HttpRequestException) { return new(false, null, MessageReseau); }
        catch (TaskCanceledException) { return new(false, null, MessageReseau); }
    }

    // ─── Sans données (le message de l'API est renvoyé tel quel) ─────────────

    public async Task<ResultatApi> PostAsync(string url, object? corps = null, string erreur = "Opération impossible.", string succes = "Opération effectuée.")
        => (await EnvoyerAsync<object>(() => Requete(HttpMethod.Post, url, corps), erreur, succes)).SansDonnees();

    public async Task<ResultatApi> PutAsync(string url, object? corps, string erreur = "Enregistrement impossible.", string succes = "Modifications enregistrées.")
        => (await EnvoyerAsync<object>(() => Requete(HttpMethod.Put, url, corps), erreur, succes)).SansDonnees();

    public async Task<ResultatApi> DeleteAsync(string url, string erreur = "Suppression impossible.", string succes = "Suppression effectuée.")
        => (await EnvoyerAsync<object>(() => new HttpRequestMessage(HttpMethod.Delete, url), erreur, succes)).SansDonnees();

    // ─── Cœur ─────────────────────────────────────────────────────────────────

    private async Task<ResultatApi<T>> EnvoyerAsync<T>(Func<HttpRequestMessage> creer, string erreur, string succes)
    {
        try
        {
            using var requete = creer();
            using var reponse = await _http.SendAsync(requete);
            var texte = await reponse.Content.ReadAsStringAsync();

            // Le corps est lu aussi en cas d'échec : certaines réponses d'erreur portent des détails (liste d'erreurs…).
            var donnees = Deserialiser<T>(texte);
            var message = ExtraireMessage(texte);

            if (!reponse.IsSuccessStatusCode)
                return new(false, donnees, MessageErreur(reponse.StatusCode, message, erreur), reponse.StatusCode);

            return new(true, donnees, message ?? succes, reponse.StatusCode);
        }
        catch (HttpRequestException) { return new(false, default, MessageReseau); }
        catch (TaskCanceledException) { return new(false, default, MessageReseau); }
    }

    private static HttpRequestMessage Requete(HttpMethod methode, string url, object? corps)
        => new(methode, url) { Content = corps is null ? null : JsonContent.Create(corps, corps.GetType()) };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static T? Deserialiser<T>(string texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return default;
        try { return JsonSerializer.Deserialize<T>(texte, Json); }
        catch { return default; }   // corps non JSON ou d'une autre forme : seules les données manquent
    }

    /// <summary>Propriété « message » d'un corps JSON objet, si elle existe.</summary>
    private static string? ExtraireMessage(string texte)
    {
        if (string.IsNullOrWhiteSpace(texte) || texte.TrimStart()[0] != '{') return null;
        try
        {
            using var doc = JsonDocument.Parse(texte);
            foreach (var p in doc.RootElement.EnumerateObject())
                if (p.NameEquals("message") || p.Name.Equals("message", StringComparison.OrdinalIgnoreCase))
                    return p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } m ? m : null;
        }
        catch { /* JSON invalide */ }
        return null;
    }

    private static string MessageErreur(HttpStatusCode statut, string? messageApi, string parDefaut)
    {
        if ((int)statut >= 500) return MessageServeur;
        if (statut == HttpStatusCode.TooManyRequests) return MessageTropDeTentatives;
        return messageApi ?? parDefaut;
    }

    /// <summary>Message exact renvoyé par l'API ({ "message": "…" }), ou le message par défaut.</summary>
    public static async Task<string> LireMessageErreurAsync(HttpResponseMessage reponse, string parDefaut)
    {
        string? message = null;
        try { message = ExtraireMessage(await reponse.Content.ReadAsStringAsync()); }
        catch { /* corps illisible */ }
        return MessageErreur(reponse.StatusCode, message, parDefaut);
    }

}

