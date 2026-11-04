namespace Finama.Web.Features.Commerce.Models;

// Types de la caisse (POS) partagés entre la page Caisse et ses composants.

// Instantané d'une vente encaissée : tout ce qu'il faut pour imprimer ou réimprimer le ticket
public record RecuCaisse
{
    public TicketPosDto Ticket { get; init; } = new();
    public string Entreprise { get; init; } = "";
    public string? Logo { get; init; }
    public string Magasin { get; init; } = "";
    public string? MagasinAdresse { get; init; }
    public string? MagasinTelephone { get; init; }
    public string Caisse { get; init; } = "";
    public string? ClientNom { get; init; }
    public string? ClientTelephone { get; init; }
    public List<LigneRecu> Lignes { get; init; } = new();
    public List<PaiementRecu> Paiements { get; init; } = new();
    public bool Reimpression { get; init; }
    public bool EstHorsLigne { get; init; }
}

public record LigneRecu(string Designation, decimal Quantite, decimal PrixUnitaire, decimal Total, string Unite);
public record PaiementRecu(string Libelle, decimal Montant, string? Reference);

/// <summary>Vente gardée sur l'appareil (même forme que dans IndexedDB).</summary>
public sealed class VenteEnFile
{
    public Guid Id { get; set; }
    public string NumeroHorsLigne { get; set; } = "";
    public DateTime DateVenteLocale { get; set; }
    public decimal Total { get; set; }
    public string Statut { get; set; } = "attente";
    public string? Erreur { get; set; }
    public CreerVentePosRequest Requete { get; set; } = new();
}

/// <summary>Ticket de la session en cours, tel que renvoyé par l'API.</summary>
public sealed record TicketSession(Guid Id, string NumeroTicket, DateTime DateVente, decimal TotalTTC, bool EstAnnule, string? ClientNom, string? MotifAnnulation);

/// <summary>Saisie de l'annulation d'un ticket (motif et, si besoin, validation d'un responsable).</summary>
public sealed class DemandeAnnulation
{
    public Guid VenteId { get; init; }
    public string Numero { get; init; } = "";
    public decimal Total { get; init; }
    public string Motif { get; set; } = "";
    public string? GerantEmail { get; set; }
    public string? GerantMotDePasse { get; set; }
}

