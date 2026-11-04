using System.Net.Http.Json;
using Finama.Web.Core.Securite;
using Finama.Web.Models;

namespace Finama.Web.Features.TableauBord.Services;

public interface ITableauBordService
{
    Task<TableauBordDto?> GetTableauBordAsync();
    Task<ResultatTableauBord> ChargerTableauBordAsync(Guid? exerciceId = null);
}

public sealed class TableauBordService : ITableauBordService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public TableauBordService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
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

            var (code, message) = await ApiClient.LireCodeErreurAsync(response);
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

}
/// <summary>Résultat du chargement du tableau de bord : données, ou code + message d'échec.</summary>
public record ResultatTableauBord(TableauBordDto? Donnees, string? Code, string? Message);