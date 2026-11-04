using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Finama.Web.Shared;
using Finama.Web.Features.Commerce.Components;

namespace Finama.Web.Features.Commerce.Pages;

/// <summary>Caisse — clôture de session, tickets de la session et annulation.</summary>
public partial class Caisse
{
    // ── Clôture ──
    private void OuvrirCloture()
    {
        if (_horsLigne || _ventesEnAttente + _ventesEnErreur > 0)
        {
            Notif.Avertir(_horsLigne
                ? "Pas de clôture hors connexion : attendez le retour du réseau."
                : $"{_ventesEnAttente + _ventesEnErreur} vente(s) faite(s) hors connexion doivent d'abord être envoyées.", "Clôture impossible");
            if (!_horsLigne) _afficherFile = true;
            return;
        }
        _declEspeces = _declCarte = _declMobile = 0;
        _commentaireCloture = null;
        _afficherCloture = true;
    }

    private async Task CloturerSession()
    {
        if (_session == null) return;
        _enCoursAction = true;
        try
        {
            var ok = await CaisseApi.CloturerSessionAsync(new CloturerSessionPosRequest
            {
                SessionCaisseId = _session.Id,
                Commentaire = _commentaireCloture,
                Lignes = new()
                {
                    new ClotureLignePosDto { Mode = ModePaiementPos.Especes, MontantDeclare = _declEspeces },
                    new ClotureLignePosDto { Mode = ModePaiementPos.Carte, MontantDeclare = _declCarte },
                    new ClotureLignePosDto { Mode = ModePaiementPos.MobileMoney, MontantDeclare = _declMobile }
                }
            });

            if (!ok) { Notif.Erreur(ErreurCommerce.Derniere ?? "Une erreur est survenue.", "Clôture impossible"); return; }

            Notif.Succes("Clôture enregistrée. Merci pour votre journée !", "Caisse clôturée");
            _afficherCloture = false;
            _panier.Clear();
            _dernierRecu = null;
            _session = null;
            await ChargerSessionAsync();
        }
        finally
        {
            _enCoursAction = false;
        }
    }

    // ── Tickets de la session & annulation ──

    private bool _afficherTickets;
    private List<TicketSession>? _tickets;
    private DemandeAnnulation? _annulation;
    private bool _enCoursAnnulation;
    private bool _estGerant;

    private async Task OuvrirTickets()
    {
        if (_session is null) return;
        _afficherTickets = true;
        _tickets = null;
        var r = await Api.GetAsync<List<TicketSession>>($"api/commerce/pos/sessions/{_session.Id}/tickets", "Liste des tickets indisponible.");
        _tickets = r.Donnees ?? new();
        if (!r.Succes) Notif.Erreur(r.Message, "Tickets");
    }

    private async Task DemarrerAnnulation(Guid venteId, string numero, decimal total)
    {
        var etat = await AuthState.GetAuthenticationStateAsync();
        await Permissions.ChargerAsync();
        _estGerant = etat.User.IsInRole("SuperAdmin") || Permissions.A("caisse.annuler");
        _annulation = new DemandeAnnulation { VenteId = venteId, Numero = numero, Total = total };
    }

    private async Task ConfirmerAnnulation()
    {
        if (_annulation is not { } an) return;
        _enCoursAnnulation = true;
        try
        {
            var r = await Api.PostAsync($"api/commerce/pos/ventes/{an.VenteId}/annuler",
                new { Motif = an.Motif.Trim(), an.GerantEmail, an.GerantMotDePasse }, "Annulation refusée.", "Ticket annulé.");

            if (!r.Succes)
            {
                Notif.Erreur(r.Message, "Annulation");
                an.GerantMotDePasse = null;
                return;
            }

            Notif.Avertir(r.Message, "Ticket annulé");
            if (_dernierRecu?.Ticket.Id == an.VenteId) _dernierRecu = null;
            _annulation = null;
            await ChargerArticles();   // stocks à jour sur les cartes
            if (_afficherTickets) await OuvrirTickets();
        }
        catch
        {
            Notif.Erreur("Serveur injoignable. Réessayez.", "Annulation");
        }
        finally
        {
            _enCoursAnnulation = false;
        }
    }

    private async Task NouvelleVente()
    {
        _dernierRecu = null;
        await FocusScannerAsync();
    }

}
