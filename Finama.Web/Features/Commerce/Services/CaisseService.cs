using Microsoft.AspNetCore.Components;

namespace Finama.Web.Features.Commerce.Services;

public interface ICaisseService
{
    Task<List<CaissePosDto>> GetCaissesAsync(Guid magasinId);
    Task<SessionPosDto?> GetSessionActiveAsync(Guid magasinId);
    Task<SessionPosDto?> OuvrirSessionAsync(OuvrirSessionPosRequest request);
    Task<TicketPosDto?> EncaisserVenteAsync(CreerVentePosRequest request);
    Task<bool> CloturerSessionAsync(CloturerSessionPosRequest request);
}

public sealed class CaisseService : CommerceServiceBase, ICaisseService
{
    public CaisseService(HttpClient http, ILocalStorage stockage, NavigationManager nav, ErreursCommerce erreurs)
        : base(http, stockage, nav, erreurs) { }

    // ─── CAISSE (POS) ────────────────────────────────────────────────────────

    public async Task<List<CaissePosDto>> GetCaissesAsync(Guid magasinId)
        => await ListeAsync<CaissePosDto>($"api/commerce/pos/caisses?magasinId={magasinId}");

    public async Task<SessionPosDto?> GetSessionActiveAsync(Guid magasinId)
        => await LireAsync<SessionPosDto>(await EnvoyerAsync(HttpMethod.Get, $"api/commerce/pos/session-active?magasinId={magasinId}"));

    public async Task<SessionPosDto?> OuvrirSessionAsync(OuvrirSessionPosRequest request)
        => await LireAsync<SessionPosDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/pos/sessions/ouvrir", request));

    public async Task<TicketPosDto?> EncaisserVenteAsync(CreerVentePosRequest request)
        => await LireAsync<TicketPosDto>(await EnvoyerAsync(HttpMethod.Post, "api/commerce/pos/ventes", request));

    public async Task<bool> CloturerSessionAsync(CloturerSessionPosRequest request)
        => Ok(await EnvoyerAsync(HttpMethod.Post, "api/commerce/pos/sessions/cloturer", request));

}
