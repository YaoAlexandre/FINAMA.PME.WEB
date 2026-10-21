using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Finama.Web.Services;

/// <summary>
/// Permissions de l'utilisateur connecté (chargées une fois par session depuis api/moi/permissions).
/// Sert aux menus et boutons ; l'API reste la vraie barrière.
/// </summary>
public class PermissionsWeb
{
    private readonly HttpClient _http;
    private Task<HashSet<string>>? _chargement;

    public PermissionsWeb(HttpClient http, AuthenticationStateProvider auth)
    {
        _http = http;
        // Connexion, déconnexion, changement de compte : les droits du compte précédent sont oubliés
        auth.AuthenticationStateChanged += _ => Reinitialiser();
    }

    public void Reinitialiser()
    {
        _chargement = null;
        Actuelles = new HashSet<string>();
    }

    public IReadOnlySet<string> Actuelles { get; private set; } = new HashSet<string>();

    public async Task<IReadOnlySet<string>> ChargerAsync(bool forcer = false)
    {
        if (forcer) _chargement = null;
        _chargement ??= LireAsync();
        Actuelles = await _chargement;
        return Actuelles;
    }

    /// <summary>Vrai si l'utilisateur a AU MOINS UNE des permissions.</summary>
    public bool A(params string[] codes) => codes.Any(Actuelles.Contains);

    private async Task<HashSet<string>> LireAsync()
    {
        try
        {
            var r = await _http.GetFromJsonAsync<Reponse>("api/moi/permissions");
            return r?.Permissions?.ToHashSet() ?? new HashSet<string>();
        }
        catch
        {
            _chargement = null;   // nouvel essai au prochain appel
            return new HashSet<string>();
        }
    }

    private sealed record Reponse(string[]? Permissions);
}