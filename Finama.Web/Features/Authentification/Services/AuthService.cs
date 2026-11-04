using System.Net.Http.Json;
using Finama.Web.Core.Securite;

namespace Finama.Web.Features.Authentification.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(string email, string motDePasse);
    Task<AuthResult?> ChangerMotDePasseAsync(ChangerMotDePasseModel model);
    Task<AuthResponse?> VerifyOtpAsync(string email, string codeOtp);
    Task LogoutAsync();
    Task<string?> GetTokenAsync();
    Task<string?> GetUserNomAsync();
    Task<string?> GetEntrepriseNomAsync();
    Task<string?> GetDeviseSymboleAsync();
    Task<bool> EstConnecteAsync();
    Task<bool> RegisterAsync(RegisterModel model);
}

public sealed class AuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;
    private readonly ILocalStorage _stockage;
    private readonly CustomAuthStateProvider _authProvider;

    public AuthService(HttpClient http, IdentifiantAppareil appareil, ILocalStorage stockage, CustomAuthStateProvider authProvider)
    {
        _http = http;
        _appareil = appareil;
        _stockage = stockage;
        _authProvider = authProvider;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string motDePasse)
    {
        // On récupère le deviceId depuis le navigateur
        var deviceId = await _appareil.ObtenirAsync();

        // On crée une requête personnalisée
        var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login");
        request.Headers.Add("X-Device-Id", deviceId);
        request.Content = JsonContent.Create(new { email, motDePasse });

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException(await ApiClient.LireMessageErreurAsync(response, "Email ou mot de passe incorrect."));

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        // Si RequiresOtp est faux, on est reconnu et connecté immédiatement
        if (result is not null && !string.IsNullOrEmpty(result.AccessToken))
        {
            await SauvegarderSessionAsync(result);
        }

        return result;
    }

    public async Task<AuthResult?> ChangerMotDePasseAsync(ChangerMotDePasseModel model)
    {
        // Récupérer le token depuis le stockage local si nécessaire
        await _appareil.GarantirAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/changer-mot-de-passe");



        request.Content = JsonContent.Create(new
        {
            AncienMotDePasseOuCode = model.AncienMotDePasseOuCode,
            NouveauMotDePasse = model.NouveauMotDePasse
        });

        var response = await _http.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var ok = await response.Content.ReadFromJsonAsync<AuthResult>();
            // Nouvelle session émise par l'API (sans le verrou "mot de passe temporaire")
            if (ok?.Session is { AccessToken.Length: > 0 } session)
                await SauvegarderSessionAsync(session);
            return ok;
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<AuthResult>();
            if (!string.IsNullOrWhiteSpace(error?.Message)) return error;
        }
        catch { /* corps non conforme */ }
        return AuthResult.Failure("Une erreur est survenue lors du changement de mot de passe.");
    }
    public async Task<AuthResponse?> VerifyOtpAsync(string email, string codeOtp)
    {
        var deviceId = await _appareil.ObtenirAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/verify-otp");
        request.Headers.Add("X-Device-Id", deviceId);
        request.Content = JsonContent.Create(new { email, codeOtp });

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            // Message exact de l'API : "Code incorrect. 3 tentative(s) restante(s).", "Trop de tentatives…", 429…
            throw new UnauthorizedAccessException(await ApiClient.LireMessageErreurAsync(response, "Code OTP invalide ou expiré."));
        }

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if (result is not null)
        {
            await SauvegarderSessionAsync(result);
        }

        return result;
    }



    public async Task LogoutAsync()
    {
        try { await _http.PostAsync("api/auth/logout", null); }
        catch { /* hors ligne : la session locale est quand même détruite */ }
        await _stockage.SupprimerAsync(ClesStockage.Session);
        _authProvider.NotifyUserLogout();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    public async Task<string?> GetTokenAsync() => await _stockage.LireAsync(ClesStockage.Jeton);
    public async Task<string?> GetUserNomAsync() => await _stockage.LireAsync(ClesStockage.Utilisateur);
    public async Task<string?> GetEntrepriseNomAsync() => await _stockage.LireAsync(ClesStockage.Entreprise);
    public async Task<string?> GetDeviseSymboleAsync() => await _stockage.LireAsync(ClesStockage.Devise);

    /// <summary>
    /// Connecté = access token encore valide, OU refresh token présent (AuthHeaderHandler renouvellera la session au premier appel).
    /// </summary>
    public async Task<bool> EstConnecteAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return false;
        if (!JwtOutils.EstExpire(token)) return true;

        var refresh = await _stockage.LireAsync(ClesStockage.Refresh);
        return !string.IsNullOrEmpty(refresh);
    }

    private async Task SauvegarderSessionAsync(AuthResponse r)
    {
        AuthHeaderHandler.ReinitialiserSession(); // nouvelle session : le verrou "session perdue" est levé
        await _stockage.EcrireAsync(ClesStockage.Jeton, r.AccessToken);
        if (!string.IsNullOrEmpty(r.RefreshToken))
            await _stockage.EcrireAsync(ClesStockage.Refresh, r.RefreshToken);
        await _stockage.EcrireAsync(ClesStockage.Utilisateur, r.NomUtilisateur);
        await _stockage.EcrireAsync(ClesStockage.Entreprise, r.NomEntreprise);
        await _stockage.EcrireAsync(ClesStockage.Devise, r.DeviseSymbole);
        _authProvider.NotifyUserAuthentication(r.AccessToken);
    }

    public async Task<bool> RegisterAsync(RegisterModel model)
    {
        try
        {
            await _appareil.GarantirAsync();
            var response = await _http.PostAsJsonAsync("api/auth/register", model);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur d'inscription : {ex.Message}");
            return false;
        }
    }

}
