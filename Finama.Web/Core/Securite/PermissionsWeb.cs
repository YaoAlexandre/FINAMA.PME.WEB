using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace Finama.Web.Core.Securite;

/// <summary>
/// Permissions de l'utilisateur connecté (chargées une fois par session depuis api/moi/permissions).
/// Sert aux menus et boutons ; l'API reste la vraie barrière.
/// </summary>
public class PermissionsWeb
{
    private const string CleCache = ClesStockage.Permissions;
    private readonly HttpClient _http;
    private readonly ILocalStorage _stockage;
    private Task<HashSet<string>>? _chargement;

    public PermissionsWeb(HttpClient http, AuthenticationStateProvider auth, ILocalStorage stockage)
    {
        _http = http;
        _stockage = stockage;
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
        var utilisateur = await UtilisateurCourantAsync();
        try
        {
            var r = await _http.GetFromJsonAsync<Reponse>("api/moi/permissions");
            var droits = r?.Permissions?.ToHashSet() ?? new HashSet<string>();
            // Copie locale : la caisse reste utilisable hors connexion avec les droits de CET utilisateur
            if (utilisateur is not null)
                await _stockage.EcrireAsync(CleCache, JsonSerializer.Serialize(new Cache(utilisateur, droits.ToArray())));
            return droits;
        }
        catch
        {
            _chargement = null;   // nouvel essai au prochain appel
            // Hors connexion : derniers droits connus, uniquement s'ils appartiennent au même utilisateur
            try
            {
                var brut = await _stockage.LireAsync(CleCache);
                var cache = string.IsNullOrEmpty(brut) ? null : JsonSerializer.Deserialize<Cache>(brut);
                if (cache is not null && utilisateur is not null && cache.Utilisateur == utilisateur) return cache.Droits.ToHashSet();
            }
            catch { }
            return new HashSet<string>();
        }
    }

    private async Task<string?> UtilisateurCourantAsync()
    {
        try
        {
            var jeton = await _stockage.LireAsync(ClesStockage.Jeton);
            if (string.IsNullOrEmpty(jeton)) return null;
            var charge = JwtOutils.LirePayload(jeton);
            return charge.TryGetValue("sub", out var sub) ? sub.GetString() : null;
        }
        catch { return null; }
    }

    private sealed record Cache(string Utilisateur, string[] Droits);

    private sealed record Reponse(string[]? Permissions);
}
