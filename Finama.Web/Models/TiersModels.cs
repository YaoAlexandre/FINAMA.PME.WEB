using System.ComponentModel.DataAnnotations;

namespace Finama.Web.Models;

public record TiersDto(
    Guid Id,
    string Code,
    string Nom,
    string Type,
    string? NINEA,
    string? Adresse,
    string? Telephone,
    string? Email,
    string? Devise,
    string? CompteNumero,
    string? CompteLibelle,
    Guid? CompteComptableId,
    bool EstActif,
    DateTime CreatedAt
);

public class CreerTiersRequest
{
    [Required(ErrorMessage = "Le nom est obligatoire")]
    public string Nom { get; set; } = string.Empty;

    [Required]
    public int Type { get; set; } // 0: Client, 1: Fournisseur, 2: Les deux

    public string? NINEA { get; set; }
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }

    [EmailAddress(ErrorMessage = "Email invalide")]
    public string? Email { get; set; }

    public string Devise { get; set; } = "FCFA";
    public Guid? CompteComptableId { get; set; }
    public Guid TenantId { get; set; }
}

public class FiltreTiersQuery
{
    public string? Type { get; set; } = null;
    public string? Recherche { get; set; } = null;
    public bool? EstActif { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
//public record FiltreTiersQuery(
//    string? Type = null, string? Recherche = null,
//    bool? EstActif = true, int Page = 1, int PageSize = 20);


public class ModifierTiersRequest
{
    public string? Code { get; set; } // Pour le titre du modal
    public string Nom { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telephone { get; set; }
    public bool EstActif { get; set; }
    public string? Adresse { get; set; }
    public string Devise { get; set; } = "FCFA";
    public string? NINEA { get; set; }
    public Guid? CompteComptableId { get; set; }
}

//public class CreerTiersRequest
//{
//    public string Nom { get; set; } = string.Empty;
//    public int Type { get; set; } // 0: Client, 1: Fournisseur
//    public string? Email { get; set; }
//    public string? Telephone { get; set; }
//    public string? NINEA { get; set; }
//    public string Devise { get; set; } = "FCFA";
//}