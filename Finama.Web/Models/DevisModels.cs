namespace Finama.Web.Models;

// ─── Enums ────────────────────────────────────────────────────────────────────
public enum StatutDevis
{
    Brouillon = 0,
    Envoye = 1,
    Accepte = 2,
    Refuse = 3,
    Expire = 4,
    Converti = 5
}

// ─── Requêtes ─────────────────────────────────────────────────────────────────
public class CreerDevisRequest
{
    public string Libelle { get; set; } = string.Empty;
    public Guid TiersId { get; set; }
    public DateTime? DateExpiration { get; set; }
    public string? Notes { get; set; }
    public List<LigneDevisRequest> Lignes { get; set; } = new();
}

public class LigneDevisRequest
{
    public string Designation { get; set; } = string.Empty;
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHT { get; set; }
    public decimal TauxTVA { get; set; }
}

// ─── Réponses ─────────────────────────────────────────────────────────────────
public class DevisDto
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public DateTime DateCreation { get; set; }
    public DateTime? DateExpiration { get; set; }
    public StatutDevis Statut { get; set; }
    public string StatutLibelle { get; set; } = string.Empty;
    public Guid TiersId { get; set; }
    public string TiersNom { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalTTC { get; set; }
    public List<LigneDevisDto> Lignes { get; set; } = new();
}

public class LigneDevisDto
{
    public Guid Id { get; set; }
    public string Designation { get; set; } = string.Empty;
    public decimal Quantite { get; set; }
    public decimal PrixUnitaireHT { get; set; }
    public decimal TauxTVA { get; set; }
    public decimal MontantHT { get; set; }
    public decimal MontantTVA { get; set; }
    public decimal MontantTTC { get; set; }
}
