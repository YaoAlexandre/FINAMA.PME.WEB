namespace Finama.Web.Models;

public class EntrepriseModel
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string SlugUnique { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PaysNom { get; set; } = string.Empty;
    public string DeviseBase { get; set; } = string.Empty;
    public decimal TauxTVA { get; set; }
    public string PlanComptableCode { get; set; } = "OHADA";
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? NumeroFiscal { get; set; }
    public string? BanqueNom { get; set; }
    public string? BanqueBIC { get; set; }

}