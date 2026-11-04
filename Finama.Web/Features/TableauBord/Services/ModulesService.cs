using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Finama.Web.Features.TableauBord.Services;

/// <summary>
/// Modules ouverts à l'entreprise connectée (menus, accueil des modules, garde de navigation).
/// Chargé une fois par session ; l'API reste la vraie barrière (403 MODULE_INACTIF).
/// </summary>
public class ModulesService
{
    private readonly HttpClient _http;
    private Task<ModulesEntreprise>? _chargement;

    public ModulesService(HttpClient http, AuthenticationStateProvider auth)
    {
        _http = http;
        // Changement de compte ou d'entreprise (mode support) : les modules sont relus
        auth.AuthenticationStateChanged += _ => Reinitialiser();
    }

    public void Reinitialiser()
    {
        _chargement = null;
        Actuels = ModulesEntreprise.Tous;
    }

    public ModulesEntreprise Actuels { get; private set; } = ModulesEntreprise.Tous;

    public async Task<ModulesEntreprise> ChargerAsync(bool forcer = false)
    {
        if (forcer) _chargement = null;
        _chargement ??= LireAsync();
        Actuels = await _chargement;
        return Actuels;
    }

    private async Task<ModulesEntreprise> LireAsync()
    {
        try
        {
            return await _http.GetFromJsonAsync<ModulesEntreprise>("api/entreprise/modules") ?? ModulesEntreprise.Tous;
        }
        catch
        {
            _chargement = null;               // nouvel essai au prochain appel
            return ModulesEntreprise.Tous;    // en cas de panne réseau on n'enlève rien : l'API protège de toute façon
        }
    }

    /// <summary>Code de module utilisé par MainLayout : COMPTA, COMMERCE, ADMIN, MODULES.</summary>
    public bool EstActif(string codeModule) => codeModule switch
    {
        "COMPTA" => Actuels.Comptabilite,
        "COMMERCE" => Actuels.Commerce,
        "VENTES" => Actuels.GestionCommerciale,
        _ => true
    };
}

/// <param name="SansEntreprise">Compte plateforme (SuperAdmin hors mode support) : aucune donnée d'entreprise.</param>
public record ModulesEntreprise(int Modules, bool Comptabilite, bool GestionCommerciale, bool Commerce, bool SansEntreprise = false)
{
    public static readonly ModulesEntreprise Tous = new(7, true, true, true);
}