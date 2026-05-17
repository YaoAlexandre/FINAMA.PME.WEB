namespace Finama.Web.Models;

public record TableauBordDto(
    int Annee,
    Guid ExerciceId,
    DateTime DateDebut,
    DateTime DateFin,
    string Devise,
    string DeviseSymbole,
    decimal ChiffreAffaires,
    decimal ChiffreAffairesMoisPrecedent,
    decimal EvolutionCA,
    decimal TotalCharges,
    decimal ResultatNet,
    decimal Tresorerie,
    int NombreFacturesEmises,
    int NombreFacturesEnAttente,
    decimal MontantFacturesEnAttente,
    int NombreClients,
    int NombreFournisseurs,
    int NombreEcrituresNonValidees,
    List<PointCaMensuelDto> CaMensuel,
    List<TopClientDto> TopClients,
    List<DerniereFactureDto> DernieresFactures
);

public record PointCaMensuelDto(
    int Mois,
    string LibelleMois,
    decimal ChiffreAffaires,
    decimal Charges
);

public record TopClientDto(
    Guid TiersId,
    string Nom,
    string Code,
    decimal TotalFacture,
    int NombreFactures
);

public record DerniereFactureDto(
    Guid Id,
    string Numero,
    string TiersNom,
    DateTime DateFacture,
    DateTime? DateEcheance,
    decimal TotalTTC,
    decimal Solde,
    string Statut,
    bool EstEnRetard
);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpireAt,
    string NomUtilisateur,
    string Email,
    string Role,
    Guid TenantId,
    string NomEntreprise,
    string Pays,
    string Devise,
    string DeviseSymbole,
    decimal TauxTVA
);
