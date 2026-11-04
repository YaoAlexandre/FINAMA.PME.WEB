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

/// <summary>Caisse — panier, client et encaissement.</summary>
public partial class Caisse
{
    // ── Panier ──
    private void AjouterAuPanier(ProduitCommerceDto art)
    {
        if (art.GereStock && art.StockActuel <= 0 && !_horsLigne)
        {
            Notif.Erreur($"« {art.Designation} » est en rupture de stock.", "Rupture");
            return;
        }

        var existant = _panier.FirstOrDefault(p => p.ProduitId == art.Id);
        if (existant != null)
        {
            if (art.GereStock && existant.Quantite + 1 > art.StockActuel && !_horsLigne)
            {
                Notif.Avertir("Impossible d'ajouter plus que le stock disponible.", "Stock limité");
                return;
            }
            existant.Quantite += 1;
        }
        else
        {
            _panier.Add(new LignePanierItem
            {
                ProduitId = art.Id,
                Designation = art.Designation,
                PrixUnitaire = art.PrixVenteTtc,
                Quantite = 1,
                StockMax = art.GereStock && !_horsLigne ? art.StockActuel : 1_000_000m,
                Vrac = art.EstVrac || (art.Unite ?? 0) != 0,   // quantité décimale : poids, litres, mètres, heures
                Unite = UniteSuffixe(art)
            });
        }
    }

    private void ModifierQuantite(LignePanierItem ligne, int delta)
    {
        var q = ligne.Quantite + delta;
        if (q > ligne.StockMax)
        {
            Notif.Avertir("Quantité supérieure au stock disponible.", "Stock limité");
            return;
        }
        ligne.Quantite = q;
        if (ligne.Quantite <= 0) _panier.Remove(ligne);
    }

    private void SaisirQuantite(LignePanierItem ligne, object? valeur)
    {
        if (!decimal.TryParse(valeur?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var q) || q <= 0)
        {
            _panier.Remove(ligne);
            return;
        }
        if (q > ligne.StockMax)
        {
            Notif.Avertir($"Maximum disponible : {ligne.StockMax:0.###}", "Stock limité");
            q = ligne.StockMax;
        }
        ligne.Quantite = q;
    }

    private void SupprimerLigne(LignePanierItem ligne) => _panier.Remove(ligne);

    private async Task DemanderViderPanier()
    {
        if (!_panier.Any()) return;

        if (!await _confirm.DemanderAsync(
                $"Les {_panier.Count} ligne(s) du ticket seront retirées. Le client sélectionné sera aussi retiré.",
                "Vider le ticket en cours ?", "Vider le ticket", type: ConfirmationType.Danger)) return;

        ViderPanier();
    }

    private void ViderPanier()
    {
        _panier.Clear();
        _client = null;
    }

    // ── Client ──
    private async Task RechercherClients(KeyboardEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_rechercheClient) || _rechercheClient.Trim().Length < 2)
        {
            _afficherResultatsClient = false;
            return;
        }
        _resultatsClients = (await ClientsApi.RechercherClientsAsync(_rechercheClient.Trim())).Where(c => c.EstActif).ToList();
        _afficherResultatsClient = true;
    }

    private void ChoisirClient(ClientCommerceDto c)
    {
        _client = c;
        _afficherResultatsClient = false;
        _rechercheClient = "";
    }

    private void RetirerClient()
    {
        _client = null;
        _paiements.RemoveAll(p => p.Mode == ModePaiementPos.Credit || p.Mode == ModePaiementPos.Points);
    }

    // ── Paiement ──
    private void OuvrirPaiement()
    {
        if (!_panier.Any()) return;
        _clientRequestId = Guid.NewGuid();   // une nouvelle vente = une nouvelle clé d'idempotence
        _paiements.Clear();
        _afficherPaiement = true;
    }

    private void FermerPaiement() => _afficherPaiement = false;

    private void AjouterPaiement(ModePaiementPos mode)
    {
        _paiements.Add(new PaiementSaisi { Mode = mode, Montant = Reste > 0 ? Reste : 0 });
    }

    private async Task ValiderVente()
    {
        if (_session == null || !PaiementValide) return;

        _enCoursEncaissement = true;
        try
        {
            var paiementsRetenus = _paiements.Where(p => p.Montant > 0).ToList();

            var requete = new CreerVentePosRequest
            {
                ClientRequestId = _clientRequestId,   // identique en cas de renvoi : jamais de doublon
                SessionCaisseId = _session.Id,
                ClientId = _client?.Id,
                Lignes = _panier.Select(l => new LigneVentePosDto { ProduitId = l.ProduitId, Quantite = l.Quantite }).ToList(),
                Paiements = paiementsRetenus.Select(p => new PaiementPosDto
                {
                    Mode = p.Mode,
                    Montant = p.Montant,
                    Reference = string.IsNullOrWhiteSpace(p.Reference) ? null : p.Reference
                }).ToList()
            };

            TicketPosDto? ticket = null;
            var horsLigne = _horsLigne;
            if (!horsLigne)
            {
                ticket = await CaisseApi.EncaisserVenteAsync(requete);
                if (ticket == null && EstErreurReseau())
                    horsLigne = _horsLigne = true;   // réseau perdu pendant l'encaissement : on garde la vente sur l'appareil
                else if (ticket == null)
                {
                    Notif.Erreur(ErreurCommerce.Derniere ?? "Une erreur est survenue.", "Échec de l'encaissement");
                    return;
                }
            }

            if (horsLigne)
            {
                if (paiementsRetenus.Any(p => p.Mode is ModePaiementPos.Credit or ModePaiementPos.Points))
                {
                    Notif.Avertir("Hors connexion, le crédit client et les points ne sont pas utilisables. Choisissez un autre paiement.", "Hors connexion");
                    return;
                }
                ticket = await MettreEnFileAsync(requete, paiementsRetenus);
                if (ticket is null) return;
            }

            // Photo de la vente AVANT de vider le panier : sert au ticket et à la réimpression
            _dernierRecu = new RecuCaisse
            {
                Ticket = ticket!,
                EstHorsLigne = horsLigne,
                Entreprise = _entrepriseNom,
                Logo = _logo,
                Magasin = _magasinNom,
                MagasinAdresse = _magasinAdresse,
                MagasinTelephone = _magasinTelephone,
                Caisse = _caisseLibelle,
                ClientNom = _client?.Nom,
                ClientTelephone = _client?.Telephone,
                Lignes = _panier.Select(l => new LigneRecu(l.Designation, l.Quantite, l.PrixUnitaire, l.Total, l.Unite)).ToList(),
                Paiements = paiementsRetenus.Select(p => new PaiementRecu(LibelleMode(p.Mode), p.Montant, p.Reference)).ToList()
            };

            _panier.Clear();
            _paiements.Clear();
            _client = null;
            _afficherPaiement = false;
            if (!horsLigne)
                _ = PrechargerRecuPdfAsync(ticket!.Id);   // prêt avant le clic : le partage reste instantané sur mobile

            if (_impressionAuto)
                await ImprimerAsync(_dernierRecu, reimpression: false);

            await ChargerArticles();
            await FocusScannerAsync();
        }
        finally
        {
            _enCoursEncaissement = false;
        }
    }

}
