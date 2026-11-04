using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace Finama.Web.Core;

/// <summary>
/// Télécharge les éditions Excel générées par l'API (api/exports/...).
/// Passe par le HttpClient authentifié (jeton + refresh automatique), puis déclenche le téléchargement côté navigateur.
/// </summary>
public class ExportService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public ExportService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    /// <summary>Renvoie null si le fichier a été téléchargé, sinon le message d'erreur à afficher.</summary>
    public async Task<string?> TelechargerAsync(string url, string nomParDefaut)
    {
        try
        {
            using var reponse = await _http.GetAsync(url);

            if (!reponse.IsSuccessStatusCode)
            {
                return reponse.StatusCode switch
                {
                    System.Net.HttpStatusCode.Forbidden => "Vous n'avez pas l'autorisation d'exporter cette édition.",
                    System.Net.HttpStatusCode.Unauthorized => "Session expirée.",
                    _ => await LireMessageAsync(reponse) ?? "L'export n'a pas pu être généré."
                };
            }

            var octets = await reponse.Content.ReadAsByteArrayAsync();
            var nom = reponse.Content.Headers.ContentDisposition?.FileNameStar
                      ?? reponse.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                      ?? nomParDefaut;
            var type = reponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/finama-fichiers.js");
            await _module.InvokeVoidAsync("telecharger", nom, type, Convert.ToBase64String(octets));
            return null;
        }
        catch (HttpRequestException)
        {
            return "Serveur injoignable. Vérifiez votre connexion.";
        }
    }

    private static async Task<string?> LireMessageAsync(HttpResponseMessage r)
    {
        try { return (await r.Content.ReadFromJsonAsync<MessageApi>())?.Message; }
        catch { return null; }
    }

    private sealed record MessageApi(string? Message);
}
