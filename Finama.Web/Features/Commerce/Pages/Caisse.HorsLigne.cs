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

/// <summary>Caisse — vente sans réseau : file locale et synchronisation.</summary>
public partial class Caisse
{
    // ══════════════ MODE HORS CONNEXION ══════════════
    private IJSObjectReference? _jsHorsLigne;
    private bool _horsLigne;
    private int _ventesEnAttente, _ventesEnErreur;
    private List<VenteEnFile> _file = new();
    private bool _afficherFile, _synchronisation;
    private readonly CancellationTokenSource _arretSynchronisation = new();

    private const string CleContexte = "contexte-caisse";
    private const string MessageReseau = "Impossible de joindre le serveur";


    /// <summary>Ce qu'il faut pour vendre sans réseau : magasin, session ouverte, catalogue (dernier chargement en ligne).</summary>
    public sealed class ContexteCaisse
    {
        public string Utilisateur { get; set; } = "";
        public Guid MagasinId { get; set; }
        public string MagasinNom { get; set; } = "";
        public string? MagasinAdresse { get; set; }
        public string? MagasinTelephone { get; set; }
        public SessionPosDto? Session { get; set; }
        public string CaisseLibelle { get; set; } = "";
        public string CaisseCode { get; set; } = "";
        public List<ProduitCommerceDto> Articles { get; set; } = new();
        public DateTime EnregistreLe { get; set; }
    }

    private bool EstErreurReseau() => ErreurCommerce.Derniere?.StartsWith(MessageReseau) == true;

    private async Task<bool> EnLigneAsync()
    {
        try { return _jsHorsLigne is null || await _jsHorsLigne.InvokeAsync<bool>("enLigne"); } catch { return true; }
    }

    private async Task<string> UtilisateurAsync()
    {
        var etat = await AuthState.GetAuthenticationStateAsync();
        return etat.User.FindFirst("sub")?.Value ?? etat.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
    }

    private async Task EnregistrerContexteLocalAsync()
    {
        if (_jsHorsLigne is null || _session is null || _magasinId is null) return;
        try
        {
            await _jsHorsLigne.InvokeVoidAsync("ecrire", CleContexte, new ContexteCaisse
            {
                Utilisateur = await UtilisateurAsync(),
                MagasinId = _magasinId.Value, MagasinNom = _magasinNom, MagasinAdresse = _magasinAdresse, MagasinTelephone = _magasinTelephone,
                Session = _session, CaisseLibelle = _caisseLibelle,
                CaisseCode = _caisses.FirstOrDefault(c => c.Id == _session.CaisseId)?.Code ?? "C",
                Articles = _articles, EnregistreLe = DateTime.UtcNow
            });
        }
        catch { /* stockage indisponible (navigation privée) : pas de mode hors connexion */ }
    }

    private async Task ChargerContexteLocalAsync()
    {
        if (_jsHorsLigne is null) return;
        try
        {
            var c = await _jsHorsLigne.InvokeAsync<ContexteCaisse?>("lire", CleContexte);
            if (c?.Session is null || c.Utilisateur != await UtilisateurAsync()) return;   // jamais la caisse d'un autre utilisateur

            _horsLigne = true;
            _magasinId = c.MagasinId; _magasinNom = c.MagasinNom; _magasinAdresse = c.MagasinAdresse; _magasinTelephone = c.MagasinTelephone;
            _session = c.Session; _caisseLibelle = c.CaisseLibelle;
            _caisses = new() { new CaissePosDto { Id = c.Session.CaisseId, Code = c.CaisseCode, Libelle = c.CaisseLibelle, SessionOuverte = true } };
            _articles = c.Articles;
            FiltrerArticles();
            Notif.Avertir($"Pas de réseau : la caisse fonctionne avec le catalogue du {c.EnregistreLe.ToLocalTime():dd/MM à HH:mm}. Les ventes seront envoyées au retour du réseau.", "Hors connexion");
        }
        catch { }
    }

    /// <summary>Garde la vente sur l'appareil et renvoie un ticket provisoire (numéro HL-CAISSE-AAMMJJ-n).</summary>
    private async Task<TicketPosDto?> MettreEnFileAsync(CreerVentePosRequest requete, List<PaiementSaisi> paiements)
    {
        if (_jsHorsLigne is null || _session is null)
        {
            Notif.Erreur("Cet appareil ne peut pas garder de ventes hors connexion (navigation privée ?).", "Hors connexion");
            return null;
        }

        var maintenant = DateTime.UtcNow;
        var code = new string((_caisses.FirstOrDefault(c => c.Id == _session.CaisseId)?.Code ?? "C").ToUpperInvariant().Where(char.IsLetterOrDigit).Take(10).ToArray());
        if (code.Length == 0) code = "C";
        var jour = maintenant.ToLocalTime().ToString("yyMMdd");
        var n = await _jsHorsLigne.InvokeAsync<int>("prochainNumero", $"compteur:{_session.CaisseId}:{jour}");
        var numero = $"HL-{code}-{jour}-{n:D4}";

        var vente = new VenteEnFile
        {
            Id = requete.ClientRequestId, NumeroHorsLigne = numero, DateVenteLocale = maintenant,
            Total = TotalPanier, Requete = requete
        };
        await _jsHorsLigne.InvokeVoidAsync("fileAjouter", vente);

        // Stock affiché : on retire ce qui vient d'être vendu (le serveur fera foi à la synchronisation)
        foreach (var l in requete.Lignes)
            if (_articles.FirstOrDefault(a => a.Id == l.ProduitId) is { GereStock: true } art) art.StockActuel -= l.Quantite;
        await EnregistrerContexteLocalAsync();
        await RafraichirFileAsync();

        Notif.Avertir($"Vente enregistrée sur l'appareil (ticket {numero}). Elle sera envoyée automatiquement au retour du réseau.", "Hors connexion");
        return new TicketPosDto
        {
            Id = requete.ClientRequestId, NumeroTicket = numero, DateVente = maintenant,
            TotalTTC = TotalPanier, MontantPaye = paiements.Sum(p => p.Montant), RenduMonnaie = Rendu
        };
    }

    private async Task RafraichirFileAsync()
    {
        if (_jsHorsLigne is null) return;
        try
        {
            _file = await _jsHorsLigne.InvokeAsync<List<VenteEnFile>>("fileLister") ?? new();
            _ventesEnAttente = _file.Count(v => v.Statut != "erreur");
            _ventesEnErreur = _file.Count(v => v.Statut == "erreur");
        }
        catch { }
        await InvokeAsync(StateHasChanged);
    }

    private async Task OuvrirFile()
    {
        await RafraichirFileAsync();
        _afficherFile = true;
    }

    private async Task ReessayerFile()
    {
        if (_jsHorsLigne is not null) await _jsHorsLigne.InvokeVoidAsync("fileRemettreEnAttente");
        await SynchroniserAsync(manuel: true);
    }

    /// <summary>Toutes les 15 secondes : retour du réseau détecté → envoi des ventes en attente, puis rechargement de la caisse.</summary>
    private async Task BoucleSynchronisationAsync()
    {
        using var minuterie = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await minuterie.WaitForNextTickAsync(_arretSynchronisation.Token))
            {
                var enLigne = await EnLigneAsync();
                if (!enLigne)
                {
                    if (!_horsLigne) { _horsLigne = true; await InvokeAsync(StateHasChanged); }
                    continue;
                }
                if (_ventesEnAttente > 0 || _horsLigne) await SynchroniserAsync(manuel: false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task SynchroniserAsync(bool manuel)
    {
        if (_synchronisation || _jsHorsLigne is null) return;
        _synchronisation = true;
        var envoyees = 0;
        var reseauOk = true;
        try
        {
            var file = await _jsHorsLigne.InvokeAsync<List<VenteEnFile>>("fileLister") ?? new();
            foreach (var v in file.Where(v => v.Statut != "erreur"))
            {
                var reponse = await Api.PostAsync<object>("api/commerce/pos/ventes/hors-ligne",
                    new { Vente = v.Requete, DateVenteLocale = v.DateVenteLocale, NumeroHorsLigne = v.NumeroHorsLigne }, erreur: "");

                if (reponse.Statut is not { } statut) { reseauOk = false; break; }   // toujours pas de réseau

                if (reponse.Succes)
                {
                    await _jsHorsLigne.InvokeVoidAsync("fileRetirer", v.Id);
                    envoyees++;
                }
                else if ((int)statut is 401 or 429 or >= 500)
                {
                    reseauOk = (int)statut != 401;
                    break;   // session expirée, serveur occupé ou en panne : on réessaiera plus tard
                }
                else
                {
                    await _jsHorsLigne.InvokeVoidAsync("fileMarquerErreur", v.Id,
                        string.IsNullOrEmpty(reponse.Message) ? $"Refusée par le serveur (erreur {(int)statut})." : reponse.Message);
                }
            }

            if (reseauOk && _horsLigne)
            {
                // Réseau revenu : on repasse en ligne et on recharge la caisse depuis le serveur
                _horsLigne = false;
                await ResoudreMagasinAsync();
                if (_magasinId.HasValue) await ChargerSessionAsync();
                if (EstErreurReseau()) _horsLigne = true;
            }
        }
        catch { /* réessai au prochain tour */ }
        finally
        {
            _synchronisation = false;
            await RafraichirFileAsync();
            if (envoyees > 0)
                Notif.Succes($"{envoyees} vente(s) faite(s) hors connexion envoyée(s) au serveur.", "Synchronisation");
            else if (manuel && !reseauOk)
                Notif.Avertir("Toujours pas de réseau : nouvel essai automatique dans quelques secondes.", "Synchronisation");
            if (_ventesEnErreur > 0 && envoyees > 0)
                Notif.Erreur($"{_ventesEnErreur} vente(s) refusée(s) par le serveur : ouvrez « à envoyer » pour le détail.", "Synchronisation");
        }
    }

}
