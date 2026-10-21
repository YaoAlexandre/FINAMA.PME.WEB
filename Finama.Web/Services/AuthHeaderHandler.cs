using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Finama.Web.Models;

namespace Finama.Web.Services;

/// <summary>
/// Point unique d'authentification HTTP :
///  • ajoute "Authorization: Bearer" et "X-Device-Id" à chaque requête ;
///  • renouvelle la session (refresh token) de façon transparente — avant envoi si le jeton est expiré, ou sur 401 ;
///  • redirige vers /changer-mot-de-passe si l'API exige un nouveau mot de passe (403 PWD_CHANGE_REQUIRED) ;
///  • redirige vers /connexion?expire=1 si la session ne peut plus être renouvelée.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private const string CleToken = "finama_token";
    private const string CleRefresh = "finama_refresh";
    private const string CleDevice = "finama_device_id";

    // Partagé entre toutes les instances : un seul refresh à la fois, même si 10 appels échouent ensemble.
    private static readonly SemaphoreSlim VerrouRefresh = new(1, 1);

    // Session définitivement perdue (refresh refusé) : plus aucune tentative, une seule redirection.
    // Réinitialisé par FinamaApiService à chaque nouvelle connexion réussie.
    private static volatile bool _sessionPerdue;
    private static int _redirectionEnCours;

    public static void ReinitialiserSession()
    {
        _sessionPerdue = false;
        Interlocked.Exchange(ref _redirectionEnCours, 0);
    }

    private readonly IJSRuntime _js;
    private readonly NavigationManager _nav;
    private readonly CustomAuthStateProvider _authProvider;

    public AuthHeaderHandler(IJSRuntime js, NavigationManager nav, CustomAuthStateProvider authProvider)
    {
        _js = js;
        _nav = nav;
        _authProvider = authProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var estAppelAuth = request.RequestUri?.AbsolutePath.Contains("/api/auth/", StringComparison.OrdinalIgnoreCase) == true;

        // Session déjà perdue : on ne sollicite plus l'API (évite la boucle refresh → 401 → refresh…)
        if (_sessionPerdue && !estAppelAuth)
        {
            await TerminerSessionAsync();
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
        }

        var token = await LireAsync(CleToken);

        // Renouvellement préventif : évite un aller-retour 401 inutile
        if (!estAppelAuth && !string.IsNullOrEmpty(token) && JwtOutils.EstExpire(token))
        {
            if (await RenouvelerAsync(request.RequestUri!, token, ct))
                token = await LireAsync(CleToken);
        }

        await AttacherAsync(request, token);
        var reponse = await base.SendAsync(request, ct);

        if (reponse.StatusCode == HttpStatusCode.Forbidden && !estAppelAuth && await ExigeChangementMotDePasseAsync(reponse))
        {
            _nav.NavigateTo("/changer-mot-de-passe");
            return reponse;
        }

        if (reponse.StatusCode != HttpStatusCode.Unauthorized || estAppelAuth)
            return reponse;

        // 401 : tentative de refresh puis rejeu unique de la requête
        if (!string.IsNullOrEmpty(token) && await RenouvelerAsync(request.RequestUri!, token, ct))
        {
            using var rejeu = await ClonerAsync(request);
            await AttacherAsync(rejeu, await LireAsync(CleToken));
            reponse.Dispose();
            return await base.SendAsync(rejeu, ct);
        }

        await TerminerSessionAsync();
        return reponse;
    }

    /// <summary>Purge la session locale, prévient Blazor et redirige UNE seule fois vers la connexion.</summary>
    private async Task TerminerSessionAsync()
    {
        _sessionPerdue = true;
        if (Interlocked.Exchange(ref _redirectionEnCours, 1) == 1) return;

        await PurgerSessionAsync();
        _authProvider.NotifyUserLogout();
        // Pas de rechargement complet : la page de connexion s'affiche aussitôt (sans repasser par l'écran de chargement).
        // Droits et modules en mémoire sont déjà vidés par NotifyUserLogout ; la connexion suivante recharge l'application.
        _nav.NavigateTo("/connexion?expire=1");
    }

    /// <summary>Renvoie vrai si un jeton valide est disponible après l'appel (rafraîchi ici ou par un appel concurrent).</summary>
    private async Task<bool> RenouvelerAsync(Uri origine, string tokenUtilise, CancellationToken ct)
    {
        await VerrouRefresh.WaitAsync(ct);
        try
        {
            var tokenActuel = await LireAsync(CleToken);

            // Un autre appel a déjà renouvelé la session pendant l'attente du verrou
            if (!string.IsNullOrEmpty(tokenActuel) && tokenActuel != tokenUtilise && !JwtOutils.EstExpire(tokenActuel))
                return true;

            if (_sessionPerdue) return false;

            var refresh = await LireAsync(CleRefresh);
            if (string.IsNullOrEmpty(tokenActuel) || string.IsNullOrEmpty(refresh))
                return false;

            var url = new Uri(new Uri(origine.GetLeftPart(UriPartial.Authority)), "/api/auth/refresh");
            using var requete = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new { AccessToken = tokenActuel, RefreshToken = refresh })
            };
            await AjouterDeviceAsync(requete);

            using var reponse = await base.SendAsync(requete, ct);
            if (!reponse.IsSuccessStatusCode)
            {
                // 401 sur /refresh = session irrécupérable : on arrête toute nouvelle tentative
                if (reponse.StatusCode == HttpStatusCode.Unauthorized) _sessionPerdue = true;
                return false;
            }

            var session = await reponse.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
            if (session is null || string.IsNullOrEmpty(session.AccessToken)) return false;

            await _js.InvokeVoidAsync("localStorage.setItem", CleToken, session.AccessToken);
            await _js.InvokeVoidAsync("localStorage.setItem", CleRefresh, session.RefreshToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
        finally
        {
            VerrouRefresh.Release();
        }
    }

    private async Task AttacherAsync(HttpRequestMessage request, string? token)
    {
        request.Headers.Authorization = string.IsNullOrEmpty(token) ? null : new AuthenticationHeaderValue("Bearer", token);
        await AjouterDeviceAsync(request);
    }

    private async Task AjouterDeviceAsync(HttpRequestMessage request)
    {
        var device = await LireAsync(CleDevice);
        request.Headers.Remove("X-Device-Id");
        if (!string.IsNullOrEmpty(device))
            request.Headers.TryAddWithoutValidation("X-Device-Id", device);
    }

    private static async Task<bool> ExigeChangementMotDePasseAsync(HttpResponseMessage reponse)
    {
        try
        {
            var contenu = await reponse.Content.ReadAsStringAsync();
            return contenu.Contains("PWD_CHANGE_REQUIRED", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private async Task PurgerSessionAsync()
    {
        foreach (var cle in new[] { CleToken, CleRefresh, "finama_user", "finama_entreprise", "finama_devise" })
            await _js.InvokeVoidAsync("localStorage.removeItem", cle);
    }

    private async Task<string?> LireAsync(string cle)
    {
        try { return await _js.InvokeAsync<string?>("localStorage.getItem", cle); }
        catch { return null; }
    }

    private static async Task<HttpRequestMessage> ClonerAsync(HttpRequestMessage source)
    {
        var copie = new HttpRequestMessage(source.Method, source.RequestUri) { Version = source.Version };

        if (source.Content is not null)
        {
            var octets = await source.Content.ReadAsByteArrayAsync();
            copie.Content = new ByteArrayContent(octets);
            foreach (var h in source.Content.Headers)
                copie.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }

        foreach (var h in source.Headers.Where(h => h.Key != "Authorization"))
            copie.Headers.TryAddWithoutValidation(h.Key, h.Value);

        foreach (var option in source.Options)
            ((IDictionary<string, object?>)copie.Options)[option.Key] = option.Value;

        return copie;
    }
}