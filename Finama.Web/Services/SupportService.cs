using Finama.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace Finama.Web.Services;

/// <summary>
/// Mode support du SuperAdmin. Pendant la session, finama_token contient le jeton support (entreprise du client) ;
/// finama_refresh garde le refresh token du SuperAdmin : à la sortie (ou à l'expiration), un refresh
/// redonne une session SuperAdmin normale, sans reconnexion.
/// </summary>
public class SupportService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private readonly NavigationManager _nav;

    public SupportService(HttpClient http, IJSRuntime js, NavigationManager nav)
    {
        _http = http;
        _js = js;
        _nav = nav;
    }

    /// <summary>Session support en cours d'après le jeton stocké, ou null.</summary>
    public async Task<SessionSupport?> LireAsync()
    {
        string? jeton;
        try { jeton = await _js.InvokeAsync<string?>("localStorage.getItem", "finama_token"); }
        catch { return null; }
        if (string.IsNullOrEmpty(jeton)) return null;

        var p = JwtOutils.LirePayload(jeton);
        if (!p.TryGetValue("support_id", out _)) return null;

        var expire = p.TryGetValue("exp", out var e) && e.TryGetInt64(out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s).LocalDateTime
            : DateTime.Now;

        return new SessionSupport(
            p.TryGetValue("tenant_nom", out var n) ? n.GetString() ?? "" : "",
            p.TryGetValue("support_ro", out var ro) && ro.GetString() == "1",
            expire);
    }

    /// <summary>Ouvre la session support. Renvoie null si OK, sinon le message d'erreur.</summary>
    public async Task<string?> DemarrerAsync(Guid tenantId, string motif, int dureeMinutes, bool lectureSeule)
    {
        try
        {
            using var r = await _http.PostAsJsonAsync($"api/superadmin/support/{tenantId}",
                new { Motif = motif, DureeMinutes = dureeMinutes, LectureSeule = lectureSeule });
            var corps = await r.Content.ReadFromJsonAsync<ReponseDemarrage>();
            if (!r.IsSuccessStatusCode || corps?.AccessToken is null)
                return corps?.Message ?? "Accès support refusé.";

            await _js.InvokeVoidAsync("localStorage.setItem", "finama_token", corps.AccessToken);
            await _js.InvokeVoidAsync("localStorage.setItem", "finama_entreprise", corps.TenantNom ?? "");
            await _js.InvokeVoidAsync("localStorage.setItem", "finama_devise", corps.DeviseSymbole ?? "FCFA");
            await _js.InvokeVoidAsync("localStorage.removeItem", "finama_magasin_id");   // le magasin d'une autre entreprise
            _nav.NavigateTo("/modules", forceLoad: true);
            return null;
        }
        catch
        {
            return "Serveur injoignable. Réessayez.";
        }
    }

    /// <summary>Quitte la session support et revient à la session SuperAdmin (sans reconnexion).</summary>
    public async Task QuitterAsync()
    {
        try { await _http.PostAsync("api/superadmin/support/terminer", null); }
        catch { /* le journal notera l'expiration */ }

        try
        {
            var jeton = await _js.InvokeAsync<string?>("localStorage.getItem", "finama_token");
            var refresh = await _js.InvokeAsync<string?>("localStorage.getItem", "finama_refresh");

            using var r = await _http.PostAsJsonAsync("api/auth/refresh", new { AccessToken = jeton, RefreshToken = refresh });
            if (r.IsSuccessStatusCode && await r.Content.ReadFromJsonAsync<AuthResponse>() is { } session)
            {
                await _js.InvokeVoidAsync("localStorage.setItem", "finama_token", session.AccessToken);
                await _js.InvokeVoidAsync("localStorage.setItem", "finama_refresh", session.RefreshToken);
                await _js.InvokeVoidAsync("localStorage.setItem", "finama_entreprise", session.NomEntreprise);
                await _js.InvokeVoidAsync("localStorage.setItem", "finama_devise", session.DeviseSymbole);
                await _js.InvokeVoidAsync("localStorage.removeItem", "finama_magasin_id");
                _nav.NavigateTo("/superadmin/tenants", forceLoad: true);
                return;
            }
        }
        catch { /* on tombe sur la reconnexion */ }

        foreach (var cle in new[] { "finama_token", "finama_refresh", "finama_magasin_id" })
            await _js.InvokeVoidAsync("localStorage.removeItem", cle);
        _nav.NavigateTo("/connexion?expire=1", forceLoad: true);
    }

    private sealed record ReponseDemarrage(string? AccessToken, string? TenantNom, string? DeviseSymbole, string? Message);
}

public record SessionSupport(string Entreprise, bool LectureSeule, DateTime Expiration);
