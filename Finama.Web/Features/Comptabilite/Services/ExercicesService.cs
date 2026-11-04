using System.Net.Http.Json;
using Finama.Web.Core.Securite;
using Finama.Web.Models;

namespace Finama.Web.Features.Comptabilite.Services;

public interface IExercicesService
{
    Task<string?> OuvrirExerciceAsync(int annee);
    Task<List<ExerciceDto>> GetExercicesAsync();
    Task<ExerciceDto?> GetExerciceCourantAsync();
    Task<bool> CloturerExerciceAsync(Guid exerciceId);
}

public sealed class ExercicesService : IExercicesService
{
    private readonly HttpClient _http;
    private readonly IdentifiantAppareil _appareil;

    public ExercicesService(HttpClient http, IdentifiantAppareil appareil)
    {
        _http = http;
        _appareil = appareil;
    }

    /// <summary>Ouvre un exercice (AdminTenant). Renvoie null si OK, sinon le message d'erreur de l'API.</summary>
    public async Task<string?> OuvrirExerciceAsync(int annee)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("api/Exercices", new { Annee = annee });
            if (response.IsSuccessStatusCode) return null;
            return (await ApiClient.LireCodeErreurAsync(response)).Message ?? "Ouverture de l'exercice impossible.";
        }
        catch
        {
            return "Serveur injoignable. Réessayez.";
        }
    }

    // ─── Exercices ────────────────────────────────────────────────────────────
    public async Task<List<ExerciceDto>> GetExercicesAsync()
    {
        await _appareil.GarantirAsync();
        var res = await _http.GetFromJsonAsync<PagedResultDto<ExerciceDto>>("api/Exercices");
        return res?.Items ?? [];
    }

    public async Task<ExerciceDto?> GetExerciceCourantAsync()
    {
        await _appareil.GarantirAsync();
        return await _http.GetFromJsonAsync<ExerciceDto>("api/Exercices/courant");
    }

    public async Task<bool> CloturerExerciceAsync(Guid exerciceId)
    {
        try
        {
            await _appareil.GarantirAsync();
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

}
