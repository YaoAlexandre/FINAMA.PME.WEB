using Finama.Web.Models;
using Microsoft.AspNetCore.Components; // 🌟 AJOUT : Requis pour NavigationManager
using Microsoft.JSInterop;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Finama.Web.Services;

public class FinamaApiService
{
    private readonly HttpClient _http;
    private readonly ILocalStorage _stockage;
    private readonly CustomAuthStateProvider _authProvider;
    private readonly NavigationManager _nav; // 🌟 CORRECTION : Déclaration du champ de navigation

    // 🌟 CORRECTION : Injection de NavigationManager dans le constructeur
    public FinamaApiService(HttpClient http, ILocalStorage stockage, NavigationManager nav, CustomAuthStateProvider authProvider)
    {
        _http = http;
        _stockage = stockage;
        _nav = nav;
        _authProvider = authProvider;
    }

    // ─── Auth ─────────────────────────────────────────────────────────────────

    // ─── Gestion du DeviceId pour l'OTP silencieux ──────────────────────────
    private async Task<string> GetDeviceIdAsync()
    {
        var id = await _stockage.LireAsync(ClesStockage.Appareil);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            await _stockage.EcrireAsync(ClesStockage.Appareil, id);
        }
        return id;
    }
    public async Task<AuthResponse?> LoginAsync(string email, string motDePasse)
    {
        // On récupère le deviceId depuis le navigateur
        var deviceId = await GetDeviceIdAsync();

        // On crée une requête personnalisée
        var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login");
        request.Headers.Add("X-Device-Id", deviceId);
        request.Content = JsonContent.Create(new { email, motDePasse });

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            throw new UnauthorizedAccessException(await LireMessageErreurAsync(response, "Email ou mot de passe incorrect."));

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
        await SetAuthHeaderAsync();
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
        var deviceId = await GetDeviceIdAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/verify-otp");
        request.Headers.Add("X-Device-Id", deviceId);
        request.Content = JsonContent.Create(new { email, codeOtp });

        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            // Message exact de l'API : "Code incorrect. 3 tentative(s) restante(s).", "Trop de tentatives…", 429…
            throw new UnauthorizedAccessException(await LireMessageErreurAsync(response, "Code OTP invalide ou expiré."));
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

    // ─── Tableau de bord ──────────────────────────────────────────────────────
    /// <summary>Compatibilité (Modules.razor…) : null si indisponible.</summary>
    public async Task<TableauBordDto?> GetTableauBordAsync()
        => (await ChargerTableauBordAsync()).Donnees;

    /// <summary>
    /// Tableau de bord avec la raison d'un échec : "SANS_TENANT", "AUCUN_EXERCICE", "EXERCICE_INTROUVABLE", "RESEAU", "ERREUR".
    /// La session expirée (401) est gérée par AuthHeaderHandler (refresh ou redirection unique) : rien à faire ici.
    /// </summary>
    public async Task<ResultatTableauBord> ChargerTableauBordAsync(Guid? exerciceId = null)
    {
        try
        {
            var url = exerciceId is { } id ? $"api/TableauBord?exerciceId={id}" : "api/TableauBord";
            using var response = await _http.GetAsync(url);

            if (response.IsSuccessStatusCode)
                return new(await response.Content.ReadFromJsonAsync<TableauBordDto>(), null, null);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return new(null, "SESSION", "Session expirée.");

            var (code, message) = await LireCodeErreurAsync(response);
            return new(null, code ?? "ERREUR", message ?? "Impossible de charger le tableau de bord.");
        }
        catch (HttpRequestException)
        {
            return new(null, "RESEAU", "Serveur injoignable. Vérifiez votre connexion.");
        }
        catch (Exception)
        {
            return new(null, "ERREUR", "Impossible de charger le tableau de bord.");
        }
    }

    /// <summary>Ouvre un exercice (AdminTenant). Renvoie null si OK, sinon le message d'erreur de l'API.</summary>
    public async Task<string?> OuvrirExerciceAsync(int annee)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("api/Exercices", new { Annee = annee });
            if (response.IsSuccessStatusCode) return null;
            return (await LireCodeErreurAsync(response)).Message ?? "Ouverture de l'exercice impossible.";
        }
        catch
        {
            return "Serveur injoignable. Réessayez.";
        }
    }

    private static async Task<(string? Code, string? Message)> LireCodeErreurAsync(HttpResponseMessage response)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string? Lire(params string[] noms)
            {
                foreach (var n in noms)
                    if (doc.RootElement.TryGetProperty(n, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
                        return v.GetString();
                return null;
            }
            return (Lire("code", "Code"), Lire("message", "Message"));
        }
        catch
        {
            return (null, null);
        }
    }

    // ─── Exercices ────────────────────────────────────────────────────────────
    public async Task<List<ExerciceDto>> GetExercicesAsync()
    {
        await SetAuthHeaderAsync();
        var res = await _http.GetFromJsonAsync<PagedResultDto<ExerciceDto>>("api/Exercices");
        return res?.Items ?? [];
    }

    public async Task<ExerciceDto?> GetExerciceCourantAsync()
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<ExerciceDto>("api/Exercices/courant");
    }

    // ─── Factures ─────────────────────────────────────────────────────────────
    public async Task<PagedResultDto<FactureDto>?> GetFacturesAsync(int page = 1, int pageSize = 20)
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<PagedResultDto<FactureDto>>(
            $"api/factures?page={page}&pageSize={pageSize}");
    }

    public async Task<FactureDto?> GetFactureAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<FactureDto>($"api/factures/{id}");
    }

    public async Task<FactureDto?> CreerFactureAsync(CreerFactureRequest request)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PostAsJsonAsync("api/factures", request);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await LireMessageErreurAsync(r, "La facture n'a pas pu être créée."));
        return await r.Content.ReadFromJsonAsync<FactureDto>();
    }

    public async Task<byte[]?> GetFacturePdfAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        var r = await _http.GetAsync($"api/factures/{id}/pdf");
        if (!r.IsSuccessStatusCode) return null;
        return await r.Content.ReadAsByteArrayAsync();
    }

    // ─── Plan comptable ───────────────────────────────────────────────────────
    public async Task<PagedResultDto<CompteComptableDto>?> GetPlanComptableAsync(
        string? classe = null, string? recherche = null, int page = 1, int pageSize = 100)
    {
        await SetAuthHeaderAsync();
        var url = $"api/PlanComptable?page={page}&pageSize={pageSize}";

        if (!string.IsNullOrEmpty(classe)) url += $"&classe={classe}";
        if (!string.IsNullOrEmpty(recherche)) url += $"&recherche={Uri.EscapeDataString(recherche)}";

        return await _http.GetFromJsonAsync<PagedResultDto<CompteComptableDto>>(url);
    }

    public async Task<CompteComptableDto?> CreerCompteAsync(CreerCompteRequest request)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PostAsJsonAsync("api/PlanComptable", request);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CompteComptableDto>();
    }

    public async Task<CompteComptableDto?> ModifierCompteAsync(Guid id, ModifierCompteRequest request)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PutAsJsonAsync($"api/PlanComptable/{id}", request);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<CompteComptableDto>();
    }

    public async Task ChangerStatutCompteAsync(Guid id, bool estActif)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PatchAsJsonAsync($"api/PlanComptable/{id}/statut", new { estActif });
        r.EnsureSuccessStatusCode();
    }

    public async Task<List<CompteSelectDto>> GetPlanComptableSelectAsync(string? classe = null)
    {
        await SetAuthHeaderAsync();
        var url = "api/PlanComptable/select";
        if (!string.IsNullOrEmpty(classe)) url += $"?classe={classe}";

        return await _http.GetFromJsonAsync<List<CompteSelectDto>>(url) ?? new();
    }
    public async Task<string> GetProchainNumeroSousCompteAsync(Guid parentId)
    {
        // Appelle le endpoint du controlleur lié à la méthode backend qu'on a implémenté
        return await _http.GetStringAsync($"api/plan-comptable/parent/{parentId}/prochain-numero");
    }
    /// <summary>
    /// Récupère la liste des comptes comptables (filtrable par classe, ex: classe 4 pour les tiers)
    /// </summary>
    public async Task<List<CompteComptableDto>> ListerComptesComptablesAsync(string? classe = null)
    {
        try
        {
            // 🌟 AJOUT INDISPENSABLE : Attacher le token JWT et le Device-Id
            await SetAuthHeaderAsync();

            // Construit l'URL avec le paramètre de requête si fourni
            var url = string.IsNullOrEmpty(classe)
                ? "api/PlanComptable/select"
                : $"api/PlanComptable/select?classe={classe}";

            var comptes = await _http.GetFromJsonAsync<List<CompteComptableDto>>(url);
            return comptes ?? new List<CompteComptableDto>();
        }
        catch (Exception)
        {
            return new List<CompteComptableDto>();
        }
    }
    // ─── Reporting ────────────────────────────────────────────────────────────
    public async Task<BalanceWrapper?> GetBalanceAsync(Guid exerciceId, string? classe = null,
                                                   DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        try
        {
            await SetAuthHeaderAsync();
            var url = $"api/Reporting/balance?exerciceId={exerciceId}";
            if (!string.IsNullOrEmpty(classe)) url += $"&classeCompte={classe}";
            if (dateDebut is { } d) url += $"&dateDebut={d:yyyy-MM-dd}";
            if (dateFin is { } f) url += $"&dateFin={f:yyyy-MM-dd}";

            return await _http.GetFromJsonAsync<BalanceWrapper>(url);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<bool> CloturerExerciceAsync(Guid exerciceId)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PostAsync($"api/exercices/{exerciceId}/cloturer", null);

            if (response.IsSuccessStatusCode) return true;

            var errorResult = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            throw new Exception(errorResult != null && errorResult.ContainsKey("message")
                ? errorResult["message"]
                : "Une erreur est survenue lors de la clôture de l'exercice.");
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<GrandLivreDto?> GetGrandLivreAsync(Guid exerciceId, string? compteNumero = null, int page = 1)
    {
        await SetAuthHeaderAsync();
        var url = $"api/reporting/grand-livre?exerciceId={exerciceId}&page={page}&pageSize=20";
        if (!string.IsNullOrEmpty(compteNumero))
            url += $"&compteNumero={Uri.EscapeDataString(compteNumero)}";
        return await _http.GetFromJsonAsync<GrandLivreDto>(url);
    }

    // ─── Écritures Comptables ──────────────────────────────────────────────────
    public async Task<PagedResult<EcritureDto>?> GetEcrituresAsync(FiltreEcritureQuery filtre)
    {
        await SetAuthHeaderAsync();
        var queryString = $"?page={filtre.Page}&pageSize={filtre.PageSize}" +
                          (!string.IsNullOrEmpty(filtre.Journal) ? $"&journal={filtre.Journal}" : "") +
                          (!string.IsNullOrEmpty(filtre.Statut) ? $"&statut={filtre.Statut}" : "");

        return await _http.GetFromJsonAsync<PagedResult<EcritureDto>>($"api/Ecritures{queryString}");
    }

    public async Task<EcritureDto?> CreerEcritureAsync(CreerEcritureRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync("api/Ecritures", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<EcritureDto>() : null;
    }

    public async Task<EcritureDto?> GetEcritureAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<EcritureDto>($"api/Ecritures/{id}");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<EcritureDto?> ModifierEcritureAsync(Guid id, CreerEcritureRequest request)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.PutAsJsonAsync($"api/Ecritures/{id}", request);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<EcritureDto>();

            var errorResult = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            throw new Exception(errorResult != null && errorResult.ContainsKey("message")
                ? errorResult["message"]
                : "Une erreur est survenue lors de la modification de l'écriture.");
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    public async Task<bool> ValiderEcritureAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PutAsync($"api/Ecritures/{id}/valider", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SupprimerEcritureAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.DeleteAsync($"api/Ecritures/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ─── Tiers ────────────────────────────────────────────────────────────────
    public async Task<PagedResultDto<TiersDto>?> GetTiersAsync(FiltreTiersQuery filtre)
    {
        await SetAuthHeaderAsync();
        var url = $"api/tiers?page={filtre.Page}&pageSize={filtre.PageSize}";
        if (!string.IsNullOrEmpty(filtre.Type)) url += $"&type={filtre.Type}";
        if (!string.IsNullOrEmpty(filtre.Recherche)) url += $"&recherche={Uri.EscapeDataString(filtre.Recherche)}";
        if (filtre.EstActif.HasValue) url += $"&estActif={filtre.EstActif.Value.ToString().ToLower()}";
        return await _http.GetFromJsonAsync<PagedResultDto<TiersDto>>(url);
    }

    public async Task<List<TiersDto>> GetTiersSelectAsync(string? type = null)
    {
        await SetAuthHeaderAsync();
        var url = "api/tiers/recherche?pageSize=100";
        if (!string.IsNullOrEmpty(type)) url += $"&type={type}";
        return await _http.GetFromJsonAsync<List<TiersDto>>(url) ?? [];
    }

    public async Task<TiersDto?> CreerTiersAsync(CreerTiersRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync("api/Tiers", request);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<TiersDto>() : null;
    }

    public async Task<bool> ModifierTiersAsync(Guid id, ModifierTiersRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PutAsJsonAsync($"api/Tiers/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SupprimerTiersAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        var response = await _http.DeleteAsync($"api/Tiers/{id}");
        return response.IsSuccessStatusCode;
    }

    // ─── Classes Comptables ─────────────────────────────────────────────────────
    public async Task<List<ClasseComptableModel>> GetClassesComptablesAsync()
    {
        await SetAuthHeaderAsync();
        return await _http.GetFromJsonAsync<List<ClasseComptableModel>>("api/ClassesComptables") ?? new();
    }

    public async Task<bool> CreerClasseComptableAsync(ClasseComptableModel model)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync("api/ClassesComptables", model);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ModifierClasseComptableAsync(int numero, string nouveauLibelle)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PutAsJsonAsync($"api/ClassesComptables/{numero}", nouveauLibelle);
        return response.IsSuccessStatusCode;
    }

    // ─── Utilisateurs & Collaborateurs ──────────────────────────────────────────
    public async Task<List<UtilisateurDto>?> GetUtilisateursTenantAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.GetFromJsonAsync<List<UtilisateurDto>>("api/Utilisateurs");
            return response ?? [];
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<string?> CreerCollaborateurAsync(CreerCollaborateurRequest model)
    {
        await SetAuthHeaderAsync();
        var response = await _http.PostAsJsonAsync("api/Utilisateurs", model);

        if (response.IsSuccessStatusCode) return null; // Pas d'erreur

        // Si on arrive ici, il y a une erreur : on lit le message renvoyé par l'API
        var errorData = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return errorData != null && errorData.ContainsKey("message")
            ? errorData["message"]
            : "Une erreur est survenue.";
    }


    public async Task<ResultatApi> ModifierStatutUtilisateurAsync(Guid id, bool actif)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PutAsJsonAsync($"api/Utilisateurs/{id}/statut", actif);
        string? message = null;
        try { message = (await r.Content.ReadFromJsonAsync<ReponseMessage>())?.Message; } catch { }
        return new ResultatApi(r.IsSuccessStatusCode,
            message ?? (r.IsSuccessStatusCode ? (actif ? "Accès réactivé." : "Accès suspendu.") : "L'opération a échoué."));
    }

    public async Task<ReinitialisationResultat> ReinitialiserMotDePasseUtilisateurAsync(Guid id)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PostAsync($"api/Utilisateurs/{id}/reinitialiser-mot-de-passe", null);
        ReponseReset? d = null;
        try { d = await r.Content.ReadFromJsonAsync<ReponseReset>(); } catch { }
        return new ReinitialisationResultat(r.IsSuccessStatusCode,
            d?.Message ?? (r.IsSuccessStatusCode ? "Mot de passe réinitialisé." : "L'opération a échoué."),
            d?.MotDePasseTemporaire);
    }

    // ─── Entreprise Profil ─────────────────────────────────────────────────────
    public async Task<EntrepriseModel?> GetEntrepriseDetailsAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            var entreprise = await _http.GetFromJsonAsync<EntrepriseModel>("api/Entreprise/profil");
            return entreprise;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task UpdateEntrepriseAsync(EntrepriseModel entreprise)
    {
        await SetAuthHeaderAsync();
        var r = await _http.PutAsJsonAsync("api/entreprise/profil", new
        {
            Nom = entreprise.Nom,
            Adresse = entreprise.Adresse,
            Telephone = entreprise.Telephone,
            NumeroFiscal = entreprise.NumeroFiscal,
            BanqueNom = entreprise.BanqueNom,
            BanqueBIC = entreprise.BanqueBIC,
            TauxTVA = entreprise.TauxTVA,
        });
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await LireMessageErreurAsync(r, "Les informations n'ont pas pu être enregistrées."));
    }

    // ─── Devises ───────────────────────────────────────────────────────────────
    public async Task<List<DeviseDto>> GetDevisesActivesAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<DeviseDto>>("api/devises") ?? new();
        }
        catch (Exception)
        {
            return new();
        }
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

    // ─── Helpers de configuration ──────────────────────────────────────────
    /// <summary>
    /// Conservé pour compatibilité : jeton et X-Device-Id sont désormais ajoutés par AuthHeaderHandler.
    /// On garantit seulement l'existence du DeviceId.
    /// </summary>
    private async Task SetAuthHeaderAsync() => await GetDeviceIdAsync();

    /// <summary>Message exact renvoyé par l'API ({ "message": "…" }), ou le message par défaut.</summary>
    private static async Task<string> LireMessageErreurAsync(HttpResponseMessage reponse, string parDefaut)
    {
        if ((int)reponse.StatusCode >= 500) return "Erreur du serveur. Réessayez dans un instant.";
        if (reponse.StatusCode == System.Net.HttpStatusCode.TooManyRequests) return "Trop de tentatives. Patientez une minute.";
        try
        {
            var corps = await reponse.Content.ReadFromJsonAsync<ReponseMessage>();
            if (!string.IsNullOrWhiteSpace(corps?.Message)) return corps.Message;
        }
        catch { /* corps non JSON */ }
        return parDefaut;
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
            await SetAuthHeaderAsync();
            var response = await _http.PostAsJsonAsync("api/auth/register", model);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur d'inscription : {ex.Message}");
            return false;
        }
    }

    public async Task<EcritureDetailDto?> GetEcritureDetailsAsync(Guid id)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _http.GetAsync($"api/Ecritures/{id}");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<EcritureDetailDto>();
            }
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<PagedResult<TiersDto>> ListerTiersAsync(FiltreTiersQuery filtre)
    {
        try
        {
            await SetAuthHeaderAsync();
            var url = $"api/Tiers?recherche={filtre.Recherche}&type={filtre.Type}&page={filtre.Page}&pageSize={filtre.PageSize}";
            return await _http.GetFromJsonAsync<PagedResult<TiersDto>>(url) ?? new PagedResult<TiersDto>();
        }
        catch (Exception)
        {
            return new PagedResult<TiersDto>();
        }
    }

    public async Task<List<PaysDto>> GetPaysAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            return await _http.GetFromJsonAsync<List<PaysDto>>("api/Pays") ?? new();
        }
        catch { return new(); }
    }


    private record ReponseMessage(string? Message);
    private record ReponseReset(string? Message, string? MotDePasseTemporaire);
}

/// <summary>Résultat du chargement du tableau de bord : données, ou code + message d'échec.</summary>
public record ResultatTableauBord(TableauBordDto? Donnees, string? Code, string? Message);