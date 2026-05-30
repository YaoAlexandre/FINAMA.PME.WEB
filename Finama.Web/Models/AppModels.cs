namespace Finama.Web.Models;

public record PagedResultDto<T>(
    List<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);


public record FactureDto(
    Guid Id, string Numero, string Type, string Statut,
    DateTime DateFacture, DateTime? DateEcheance, string Devise,
    string EntrepriseNom, string? EntrepriseAdresse,
    string? EntrepriseTelephone, string EntrepriseEmail,
    string? EntrepriseNumeroFiscal, string EntreprisePays,
    string EntrepriseDeviseSymbole,
    string TiersNom, string? TiersAdresse, string? TiersTelephone,
    string? TiersEmail, string? TiersNumeroFiscal, string TiersCode,
    List<LigneFactureDto> Lignes,
    decimal TotalHT, decimal TotalTVA, decimal TotalTTC,
    decimal MontantRegle, decimal Solde, string? Notes);

public record LigneFactureDto(
    string Description, decimal Quantite, decimal PrixUnitaireHT,
    decimal TauxTVA, decimal MontantHT, decimal MontantTVA, decimal MontantTTC);

public record CreerFactureRequest(
    int Type, DateTime DateFacture, DateTime? DateEcheance,
    Guid TiersId, Guid ExerciceId, string? Notes,
    List<CreerLigneFactureRequest> Lignes);

public record CreerLigneFactureRequest(
    string Description, decimal Quantite,
    decimal PrixUnitaireHT, decimal TauxTVA,
    Guid? CompteProduitsId = null);

public record CompteComptableDto(
    Guid Id, string Numero, string Libelle,
    int Classe, string LibelleClasse, string Type,
    bool EstSysteme, bool EstActif,
    Guid? CompteParentId, string? CompteParentNumero,
    int NombreSousComptes);

public record CreerCompteRequest(
    string Numero, string Libelle, int Classe,
    string Type, Guid? CompteParentId);

public record ModifierCompteRequest(
    string Libelle, bool EstActif, Guid? CompteParentId);

//public record BalanceDto(
//    Guid ExerciceId, int Annee,
//    DateTime DateDebut, DateTime DateFin,
//    DateTime? FiltreDebut, DateTime? FiltreFin,
//    List<LigneBalanceDto> Lignes,
//    TotauxBalanceDto Totaux);

public record LigneBalanceDto(
    Guid CompteId, string Numero, string Libelle, int Classe,
    decimal SoldeOuvertureDebit, decimal SoldeOuvertureCredit,
    decimal MouvementsDebit, decimal MouvementsCredit,
    decimal SoldeFinalDebit, decimal SoldeFinalCredit);

public record TotauxBalanceDto(
    decimal TotalOuvertureDebit, decimal TotalOuvertureCredit,
    decimal TotalMouvementsDebit, decimal TotalMouvementsCredit,
    decimal TotalSoldeFinalDebit, decimal TotalSoldeFinalCredit);

public record GrandLivreDto(
    Guid ExerciceId, int Annee,
    List<CompteGrandLivreDto> Comptes);

public record CompteGrandLivreDto(
    Guid CompteId, string Numero, string Libelle,
    decimal SoldeOuverture,
    List<MouvementGrandLivreDto> Mouvements,
    decimal TotalDebit, decimal TotalCredit, decimal SoldeFinal);

public record MouvementGrandLivreDto(
    Guid EcritureId, string Reference,
    DateTime Date, string Journal, string Libelle,
    string? TiersNom, decimal Debit, decimal Credit, decimal SoldeCumule);

//public record ExerciceDto(
//    Guid Id, int Annee,
//    DateTime DateDebut, DateTime DateFin, bool EstCloture);

public record ExerciceDto(Guid Id, int Annee, DateTime DateDebut, DateTime DateFin, bool EstCloture);

public record BalanceWrapper(BalanceDto Rapport, bool EstEquilibree, string? Avertissement);

public record BalanceDto(Guid ExerciceId, int Annee, List<BalanceLigneDto> Lignes, BalanceTotauxDto Totaux);

public record BalanceLigneDto(
    string Numero, string Libelle, int Classe,
    decimal SoldeOuvertureDebit, decimal SoldeOuvertureCredit,
    decimal MouvementsDebit, decimal MouvementsCredit,
    decimal SoldeFinalDebit, decimal SoldeFinalCredit);

public record BalanceTotauxDto(
    decimal TotalOuvertureDebit, decimal TotalOuvertureCredit,
    decimal TotalMouvementsDebit, decimal TotalMouvementsCredit,
    decimal TotalSoldeFinalDebit, decimal TotalSoldeFinalCredit);

public record DeviseDto(string Code, string Symbole, string Libelle, decimal TauxBaseDollar);

public record EntrepriseUpdateRequest(
    string Nom,
    string? Adresse,
    string? Telephone,
    string? NumeroFiscal,
    string? BanqueNom,
    string? BanqueBIC
);