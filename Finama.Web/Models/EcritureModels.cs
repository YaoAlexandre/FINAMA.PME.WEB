namespace Finama.Web.Models;

public class CreerEcritureRequest
{
    public Guid ExerciceId { get; set; }
    public DateTime DateEcriture { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public string Journal { get; set; } = string.Empty; // Code du journal (ex: VT, AC, BQ)
    public Guid? FactureId { get; set; }
    public List<CreerLigneEcritureRequest> Lignes { get; set; } = new();
}

public class CreerLigneEcritureRequest
{
    public Guid CompteId { get; set; }
    public Guid? TiersId { get; set; } // Pour la comptabilité tiers (Fournisseurs/Clients)
    public string Libelle { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string Devise { get; set; } = "XOF";
    public decimal? TauxChange { get; set; } = 1;
}


public record EcritureDto(
    Guid Id,
    string Reference,
    DateTime DateEcriture,
    string Libelle,
    string Journal,
    string Statut,
    Guid ExerciceId,
    decimal TotalDebit,
    decimal TotalCredit,
    bool EstEquilibree,
    string UtilisateurNom,
    DateTime CreatedAt,
    List<LigneEcritureDto> Lignes
);

public record LigneEcritureDto(
    Guid Id,
    string CompteNumero,
    string CompteLibelle,
    string? TiersNom,
    Guid CompteId,
    string Libelle,
    decimal Debit,
    decimal Credit,
    string Devise
);

public class FiltreEcritureQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Journal { get; set; }
    public string? Statut { get; set; }
    public DateTime? DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
}

public record EcritureDetailDto(
    Guid Id, DateTime DateEcriture, string Reference,
    string Journal, string Libelle, string Statut,
    decimal TotalDebit, decimal TotalCredit,
    List<LigneEcritureDetailDto> Lignes);

public record LigneEcritureDetailDto(
    Guid Id, string CompteNumero, string CompteLibelle,
    string Libelle, decimal Debit, decimal Credit);
// Pour gérer la réponse paginée
//public record PagedResult<T>(
//    List<T> Items,
//    int Page,
//    int PageSize,
//    int TotalItems,
//    int TotalPages
//);