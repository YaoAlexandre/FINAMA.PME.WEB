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

/// <summary>Caisse — impression du ticket et envoi du reçu (WhatsApp / PDF).</summary>
public partial class Caisse
{
    // ── Ticket : impression / partage ──
    private async Task ImprimerDernierTicket()
    {
        if (_dernierRecu != null)
            await ImprimerAsync(_dernierRecu, reimpression: true);
    }

    private async Task ImprimerAsync(RecuCaisse recu, bool reimpression)
    {
        if (_jsPos is null)
        {
            Notif.Avertir("Impression indisponible dans ce navigateur.", "Ticket");
            return;
        }

        _recuAImprimer = recu with { Reimpression = reimpression };

        // QR du ticket : numéro, date et montant, lisibles par n'importe quel téléphone (preuve d'achat, retour, facture sur demande)
        try
        {
            _jsCodes ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/finama-codes.js");
            var t = recu.Ticket;
            _qrTicket = await _jsCodes.InvokeAsync<string>("qrSvg",
                $"{(string.IsNullOrWhiteSpace(recu.Entreprise) ? recu.Magasin : recu.Entreprise)}\nTicket {t.NumeroTicket}\n{t.DateVente.ToLocalTime():dd/MM/yyyy HH:mm}\nTotal {t.TotalTTC:N0} FCFA");
        }
        catch { _qrTicket = null; /* ticket imprimé sans QR */ }

        StateHasChanged();          // rend le ticket dans la zone d'impression
        await Task.Yield();

        try { await _jsPos.InvokeVoidAsync("imprimerTicket"); }
        catch { Notif.Avertir("L'impression n'a pas pu être lancée.", "Ticket"); }
    }

    // ── Reçu : WhatsApp direct au client (texte + lien PDF) ou partage du fichier PDF ──
    private IJSObjectReference? _jsPartage;
    private (Guid VenteId, string Base64)? _recuPdf;
    private (Guid VenteId, string Url)? _lienRecu;
    private bool _envoiRecu;

    private sealed record LienRecu(string Url);

    /// <summary>Juste après la vente : PDF et lien préparés en arrière-plan, pour que le clic ouvre tout instantanément
    /// (sinon le téléphone bloque le partage et l'ordinateur bloque l'ouverture de WhatsApp).</summary>
    private async Task PrechargerRecuPdfAsync(Guid venteId)
    {
        var pdf = await Api.GetOctetsAsync($"api/commerce/pos/ventes/{venteId}/recu");
        if (pdf is { Succes: true, Donnees: { } octets })   // sinon : sera téléchargé au clic
            _recuPdf = (venteId, Convert.ToBase64String(octets));

        var lien = await Api.PostAsync<LienRecu>($"api/commerce/pos/ventes/{venteId}/lien-recu", null);
        if (lien is { Succes: true, Donnees.Url: { } url })   // sinon : message sans lien
            _lienRecu = (venteId, url);
    }

    /// <summary>Détail du ticket, envoyé avec le PDF (légende du fichier dans WhatsApp).</summary>
    private static string MessageRecu(RecuCaisse r, string? lien = null)
    {
        var texte = new System.Text.StringBuilder()
            .AppendLine(string.IsNullOrWhiteSpace(r.Entreprise) ? r.Magasin : r.Entreprise)
            .AppendLine($"Ticket {r.Ticket.NumeroTicket} du {r.Ticket.DateVente.ToLocalTime():dd/MM/yyyy HH:mm}");
        foreach (var l in r.Lignes)
            texte.AppendLine($"- {l.Designation} : {l.Quantite:0.###} x {l.PrixUnitaire:N0} = {l.Total:N0}");
        texte.AppendLine($"TOTAL : {r.Ticket.TotalTTC:N0} FCFA");
        if (!string.IsNullOrEmpty(lien))
            texte.AppendLine().AppendLine($"Téléchargez votre reçu (PDF) : {lien}");
        texte.Append("Merci de votre visite !");
        return texte.ToString();
    }

    /// <summary>
    /// Téléphone / tablette : le menu de partage s'ouvre avec le FICHIER PDF + le détail du ticket en message ;
    /// on choisit WhatsApp puis le client. Ordinateur (pas de partage de fichiers) : conversation WhatsApp du client
    /// avec le détail et le lien du PDF.
    /// </summary>
    private async Task EnvoyerRecu()
    {
        if (_dernierRecu is not { } r || _envoiRecu) return;
        _envoiRecu = true;
        try
        {
            _jsPartage ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/finama-partage.js");

            if (r.EstHorsLigne)
            {
                // Ticket provisoire : pas encore de PDF sur le serveur → détail du ticket par WhatsApp
                var tel = new string((r.ClientTelephone ?? "").Where(char.IsDigit).ToArray());
                if (tel.Length == 8) tel = "228" + tel;
                await _jsPartage.InvokeVoidAsync("ouvrir", $"https://wa.me/{tel}?text={Uri.EscapeDataString(MessageRecu(r))}");
                return;
            }

            if (_recuPdf is not { } pdf || pdf.VenteId != r.Ticket.Id)
            {
                await PrechargerRecuPdfAsync(r.Ticket.Id);
                pdf = _recuPdf ?? default;
            }

            if (pdf.VenteId == r.Ticket.Id && !string.IsNullOrEmpty(pdf.Base64))
            {
                var resultat = await _jsPartage.InvokeAsync<string>("partagerPdf", pdf.Base64, $"Recu_{r.Ticket.NumeroTicket}.pdf", MessageRecu(r));
                if (resultat is "partage" or "annule") return;
            }

            // Ordinateur : conversation du client (ou choix du contact) avec le détail + le lien du PDF
            var numero = new string((r.ClientTelephone ?? "").Where(char.IsDigit).ToArray());
            if (numero.Length == 8) numero = "228" + numero;   // numéro local togolais → indicatif +228
            var lien = _lienRecu is { } l && l.VenteId == r.Ticket.Id ? l.Url : null;
            await _jsPartage.InvokeVoidAsync("ouvrir", $"https://wa.me/{numero}?text={Uri.EscapeDataString(MessageRecu(r, lien))}");
        }
        catch (Exception)
        {
            Notif.Erreur("Le reçu n'a pas pu être envoyé. Réessayez.", "Envoi du reçu");
        }
        finally
        {
            _envoiRecu = false;
        }
    }

    private async Task BasculerImpressionAuto(ChangeEventArgs e)
    {
        _impressionAuto = e.Value is true;
        try { await Stockage.EcrireAsync(ClesStockage.ImpressionAuto, _impressionAuto ? "1" : "0"); }
        catch { /* préférence non mémorisée */ }
    }

    private async Task FocusScannerAsync()
    {
        try { if (_jsPos is not null) await _jsPos.InvokeVoidAsync("focusScanner"); }
        catch { /* sans conséquence */ }
    }

}
