using System.Text.Json;
using Microsoft.JSInterop;

namespace Finama.Web.Core.Stockage;

/// <summary>
/// Accès unique au localStorage du navigateur. Ne lève jamais d'exception :
/// une lecture impossible (navigation privée, quota, JS indisponible) renvoie null,
/// une écriture impossible est ignorée.
/// </summary>
public interface ILocalStorage
{
    Task<string?> LireAsync(string cle);

    /// <summary>Écrit la valeur ; une valeur null supprime la clé (plutôt que de stocker « null »).</summary>
    Task EcrireAsync(string cle, string? valeur);

    Task SupprimerAsync(params string[] cles);

    Task<T?> LireJsonAsync<T>(string cle);

    Task EcrireJsonAsync<T>(string cle, T valeur);
}

public sealed class LocalStorage : ILocalStorage
{
    private readonly IJSRuntime _js;

    public LocalStorage(IJSRuntime js) => _js = js;

    public async Task<string?> LireAsync(string cle)
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", cle); }
        catch { return null; }
    }

    public async Task EcrireAsync(string cle, string? valeur)
    {
        if (valeur is null) { await SupprimerAsync(cle); return; }
        try { await _js.InvokeVoidAsync("localStorage.setItem", cle, valeur); }
        catch { /* stockage indisponible : la donnée n'est simplement pas mémorisée */ }
    }

    public async Task SupprimerAsync(params string[] cles)
    {
        foreach (var cle in cles)
        {
            try { await _js.InvokeVoidAsync("localStorage.removeItem", cle); }
            catch { /* stockage indisponible */ }
        }
    }

    public async Task<T?> LireJsonAsync<T>(string cle)
    {
        var brut = await LireAsync(cle);
        if (string.IsNullOrEmpty(brut)) return default;
        try { return JsonSerializer.Deserialize<T>(brut); }
        catch { return default; }   // cache corrompu ou d'un ancien format
    }

    public Task EcrireJsonAsync<T>(string cle, T valeur) => EcrireAsync(cle, JsonSerializer.Serialize(valeur));
}
