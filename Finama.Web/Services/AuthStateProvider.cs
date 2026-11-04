using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Security.Claims;
using System.Text.Json;

namespace Finama.Web.Services;

/// <summary>
/// État d'authentification Blazor construit depuis le JWT stocké (ClesStockage.Jeton).
/// Jeton expiré SANS refresh token → anonyme (redirection connexion).
/// Jeton expiré AVEC refresh token → reste connecté : AuthHeaderHandler renouvelle au premier appel API.
/// </summary>
public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonyme = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly ILocalStorage _stockage;

    public CustomAuthStateProvider(ILocalStorage stockage)
    {
        _stockage = stockage;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string? token, refresh;
        try
        {
            token = await _stockage.LireAsync(ClesStockage.Jeton);
            refresh = await _stockage.LireAsync(ClesStockage.Refresh);
        }
        catch
        {
            return Anonyme; // JS indisponible (prérendu)
        }

        if (string.IsNullOrEmpty(token)) return Anonyme;
        if (JwtOutils.EstExpire(token, margeSecondes: 0) && string.IsNullOrEmpty(refresh)) return Anonyme;

        return new AuthenticationState(CreerPrincipal(token));
    }

    public void NotifyUserAuthentication(string token)
        => NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CreerPrincipal(token))));

    public void NotifyUserLogout()
        => NotifyAuthenticationStateChanged(Task.FromResult(Anonyme));

    private static ClaimsPrincipal CreerPrincipal(string token)
    {
        var claims = new List<Claim>();

        foreach (var (cle, valeur) in JwtOutils.LirePayload(token))
        {
            var type = cle switch
            {
                "role" => ClaimTypes.Role,
                "nom" => ClaimTypes.Name,
                "sub" => ClaimTypes.NameIdentifier,
                _ => cle
            };

            // Un claim peut être un tableau (plusieurs rôles) : un Claim par valeur
            if (valeur.ValueKind == JsonValueKind.Array)
                claims.AddRange(valeur.EnumerateArray().Select(v => new Claim(type, v.ToString())));
            else
                claims.Add(new Claim(type, valeur.ToString()));

            // Conserve aussi le nom brut (compatibilité avec le code existant qui lit "nom", "sub", "role")
            if (type != cle && valeur.ValueKind != JsonValueKind.Array)
                claims.Add(new Claim(cle, valeur.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", ClaimTypes.Name, ClaimTypes.Role));
    }
}
