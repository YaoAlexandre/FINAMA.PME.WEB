using System.Text.Json.Serialization;

namespace Finama.Web.Features.Commerce.Models;

// ─── DTOs Dédiés au Commerce ──────────────────────────────────────────────

public class ProduitCommerceDto
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";

    [JsonIgnore]
    public string Designation
    {
        get => Libelle;
        set => Libelle = value;
    }
    public string Libelle { get; set; } = "";

    public string? Description { get; set; }
    public int Type { get; set; }               // 0 Standard, 1 Vrac, 2 Livre, 3 Service
    public int? Unite { get; set; }             // 0 Pièce, 1 Kg, 2 g, 3 L, 4 m, 5 Heure
    public Guid? CategorieId { get; set; }
    public string? CategorieNom { get; set; }

    [JsonIgnore] public bool IsLivre => Type == 2;
    [JsonIgnore] public bool EstVrac => Type == 1;
    [JsonIgnore] public bool EstService => Type == 3;

    public decimal PrixAchat { get; set; }
    public decimal PrixVenteTTC { get; set; }

    [JsonIgnore]
    public decimal PrixVenteTtc
    {
        get => PrixVenteTTC;
        set => PrixVenteTTC = value;
    }

    public decimal TauxTva { get; set; } = 18m;
    public decimal StockActuel { get; set; }
    public decimal SeuilReappro { get; set; }

    [JsonIgnore]
    public decimal StockAlerte
    {
        get => SeuilReappro;
        set => SeuilReappro = value;
    }

    public bool SuiviLots { get; set; }
    public bool GereStock { get; set; } = true;
    public bool EstActif { get; set; } = true;
    public string? CodeBarres { get; set; }
    public string? Isbn { get; set; }
    public string? Auteur { get; set; }
    public string? Editeur { get; set; }        // lecture seule (nom) ; on envoie EditeurId
    public Guid? EditeurId { get; set; }
    public Dictionary<string, string> Attributs { get; set; } = new();   // valeurs des champs personnalisés de la catégorie
}

public class StockLotDto
{
    public Guid Id { get; set; }
    public Guid ProduitId { get; set; }
    public string DesignationProduit { get; set; } = "";
    public string NumeroLot { get; set; } = "";
    public DateTime? DateExpiration { get; set; }
    public decimal QuantiteDisponible { get; set; }
}

// ─── POS ──────────────────────────────────────────────────────────────────

public enum ModePaiementPos { Especes = 1, Carte = 2, MobileMoney = 3, Credit = 4, Points = 5 }

public class CaissePosDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public bool SessionOuverte { get; set; }
}

public class SessionPosDto
{
    public Guid Id { get; set; }
    public Guid CaisseId { get; set; }
    public Guid MagasinId { get; set; }
    public DateTime DateOuverture { get; set; }
    public decimal FondDeCaisse { get; set; }
}

public class OuvrirSessionPosRequest
{
    public Guid CaisseId { get; set; }
    public decimal FondDeCaisse { get; set; }
}

public class CreerVentePosRequest
{
    public Guid ClientRequestId { get; set; }
    public Guid SessionCaisseId { get; set; }
    public Guid? ClientId { get; set; }
    public List<LigneVentePosDto> Lignes { get; set; } = new();
    public List<PaiementPosDto> Paiements { get; set; } = new();
    public AutorisationPosDto? Autorisation { get; set; }
}

public class LigneVentePosDto
{
    public Guid ProduitId { get; set; }
    public decimal Quantite { get; set; }
    public decimal? RemisePourcent { get; set; }
}

public class PaiementPosDto
{
    public ModePaiementPos Mode { get; set; }
    public decimal Montant { get; set; }
    public string? Operateur { get; set; }
    public string? Reference { get; set; }
}

public class AutorisationPosDto
{
    public Guid SuperviseurId { get; set; }
    public string? Pin { get; set; }
}

public class TicketPosDto
{
    public Guid Id { get; set; }
    public string NumeroTicket { get; set; } = "";
    public DateTime DateVente { get; set; }
    public decimal TotalHT { get; set; }
    public decimal TotalTVA { get; set; }
    public decimal TotalRemise { get; set; }
    public decimal TotalTTC { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal RenduMonnaie { get; set; }
}

public class ClotureLignePosDto
{
    public ModePaiementPos Mode { get; set; }
    public decimal MontantDeclare { get; set; }
}

public class CloturerSessionPosRequest
{
    public Guid SessionCaisseId { get; set; }
    public List<ClotureLignePosDto> Lignes { get; set; } = new();
    public string? Commentaire { get; set; }
}

// ─── CLIENTS ──────────────────────────────────────────────────────────────

public enum TypeClientCommerce { Particulier = 1, Entreprise = 2 }

public class ClientCommerceDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Nom { get; set; } = "";
    public TypeClientCommerce Type { get; set; } = TypeClientCommerce.Particulier;
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Adresse { get; set; }
    public string? Nif { get; set; }
    public decimal PlafondCredit { get; set; }
    public decimal SoldeCredit { get; set; }
    public int PointsFidelite { get; set; }
    public bool EstActif { get; set; } = true;
}

public class SaveClientCommerceRequest
{
    public string Nom { get; set; } = "";
    public TypeClientCommerce Type { get; set; } = TypeClientCommerce.Particulier;
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? Adresse { get; set; }
    public string? Nif { get; set; }
    public decimal PlafondCredit { get; set; }
}

public class ClientDetailCommerceDto
{
    public ClientCommerceDto Client { get; set; } = new();
    public decimal TotalAchats { get; set; }
    public List<AchatClientCommerceDto> DerniersAchats { get; set; } = new();
    public List<MouvementFideliteCommerceDto> Fidelite { get; set; } = new();
}

public class AchatClientCommerceDto
{
    public Guid VenteId { get; set; }
    public string NumeroTicket { get; set; } = "";
    public DateTime DateVente { get; set; }
    public decimal TotalTTC { get; set; }
}

public class MouvementFideliteCommerceDto
{
    public int Type { get; set; }               // 1 gain, 2 utilisation, 3 ajustement, 4 expiration
    public int Points { get; set; }
    public int SoldeApres { get; set; }
    public DateTime DateMouvement { get; set; }
    public string? Motif { get; set; }
}

// ─── CATÉGORIES & ÉDITEURS ────────────────────────────────────────────────

public class ChampPersonnaliseDto
{
    public string Nom { get; set; } = "";
    public string Type { get; set; } = "texte";          // texte | nombre | liste | date
    public bool Obligatoire { get; set; }
    public List<string>? Options { get; set; }
}

public class CategorieCommerceDto
{
    public Guid Id { get; set; }
    public string Libelle { get; set; } = "";
    public Guid? ParentId { get; set; }
    public int NbProduits { get; set; }
    public List<ChampPersonnaliseDto> Champs { get; set; } = new();
}

public class SaveCategorieCommerceRequest
{
    public string Libelle { get; set; } = "";
    public Guid? ParentId { get; set; }
    public List<ChampPersonnaliseDto> Champs { get; set; } = new();
}

public class TableauBordCommerceDto
{
    public bool ActiviteConfiguree { get; set; }
    public bool UtiliseLots { get; set; }
    public int NbMagasins { get; set; }
    public int NbPointsDeVente { get; set; }
    public int NbArticles { get; set; }
    public int NbClients { get; set; }
    public bool SessionOuverte { get; set; }
    public int NbVentesJour { get; set; }
    public decimal ChiffreAffairesJour { get; set; }
    public int ArticlesSousSeuil { get; set; }
    public int LotsPerimes { get; set; }
    public int LotsProchesPeremption { get; set; }
    public decimal CreancesClients { get; set; }
    public int NbClientsEnCredit { get; set; }
}

public class ParametresCommerceDto
{
    public string? Activite { get; set; }
    public bool UtiliseLots { get; set; }
    public bool UtiliseVrac { get; set; }
    public bool UtiliseLivres { get; set; }
    public decimal TauxTvaDefaut { get; set; } = 18;
    public decimal MontantParPoint { get; set; }
    public decimal ValeurPointFcfa { get; set; }
    public decimal SeuilRemiseSansPinPourcent { get; set; }
    public decimal RemiseMaxPourcent { get; set; }
    public bool AutoriserStockNegatif { get; set; }
    public string PrefixeTicket { get; set; } = "T";
}

public class ProfilActiviteDto
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public string Description { get; set; } = "";
    public bool UtiliseLots { get; set; }
    public bool UtiliseVrac { get; set; }
    public bool UtiliseLivres { get; set; }
    public List<string> CategoriesTypes { get; set; } = new();
}

public class EditeurCommerceDto
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Contact { get; set; }
    public bool EstActif { get; set; } = true;
    public int NbTitres { get; set; }
}

public class SaveEditeurCommerceRequest
{
    public string Nom { get; set; } = "";
    public string? Contact { get; set; }
}

// ─── DTOs pour les Inventaires ────────────────────────────────────────────

public class OuvrirInventaireRequest
{
    public Guid MagasinId { get; set; } = Guid.Empty;
    public string Libelle { get; set; } = "Inventaire Général";
    public Guid? CategorieId { get; set; }

    public OuvrirInventaireRequest() { }

    public OuvrirInventaireRequest(Guid magasinId, string libelle, Guid? categorieId)
    {
        MagasinId = magasinId;
        Libelle = libelle;
        CategorieId = categorieId;
    }
}

public class InventaireLigneDto
{
    public Guid LigneId { get; set; }
    public Guid ProduitId { get; set; }
    public string ProduitLibelle { get; set; } = "";
    public Guid? LotId { get; set; }
    public string? NumeroLot { get; set; }
    public decimal? QuantiteTheorique { get; set; }
    public decimal? QuantiteComptee { get; set; }
    public decimal? Ecart { get; set; }

    public InventaireLigneDto() { }

    public InventaireLigneDto(
        Guid ligneId,
        Guid produitId,
        string produitLibelle,
        Guid? lotId,
        string? numeroLot,
        decimal? quantiteTheorique,
        decimal? quantiteComptee,
        decimal? ecart)
    {
        LigneId = ligneId;
        ProduitId = produitId;
        ProduitLibelle = produitLibelle;
        LotId = lotId;
        NumeroLot = numeroLot;
        QuantiteTheorique = quantiteTheorique;
        QuantiteComptee = quantiteComptee;
        Ecart = ecart;
    }
}

public record InventaireDto(
    Guid Id,
    string Numero,
    string Libelle,
    StatutInventaire Statut,
    DateTime DateOuverture,
    DateTime? DateCloture,
    IReadOnlyList<InventaireLigneDto> Lignes
);

public record SaisieLigneComptage(Guid LigneId, decimal QuantiteComptee);
public record ComptageRequest(List<SaisieLigneComptage> Lignes);
public record ClotureInventaireRequest(bool TraiterNonComptesCommeZero);

public enum TypeMagasin { PointDeVente = 1, Entrepot = 2, Transit = 3 }

public record MagasinDto(
    Guid Id,
    string Code,
    string Nom,
    TypeMagasin Type,
    string? Adresse,
    string? Telephone,
    string? Responsable,
    bool EstPrincipal,
    bool EstActif
);

public class CreerMagasinRequest
{
    public string Code { get; set; } = "";
    public string Nom { get; set; } = "";
    public TypeMagasin Type { get; set; } = TypeMagasin.PointDeVente;
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Responsable { get; set; }
    public bool EstPrincipal { get; set; }
}

public enum StatutInventaire
{
    Ouvert = 0,
    Cloture = 1,
    Annule = 2
}
