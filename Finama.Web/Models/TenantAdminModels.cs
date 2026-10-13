namespace Finama.Web.Models;

public class TenantAdminDto
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string EmailAdmin { get; set; } = string.Empty;
    public DateTime DateCreation { get; set; }
    public bool Actif { get; set; }
    public int NombreUtilisateurs { get; set; }
}

public class ChangerStatutTenantRequest
{
    public bool Actif { get; set; }
}

public class DashboardStatsDto
{
    public int TotalTenants { get; set; }
    public int TenantsActifs { get; set; }
    public int TenantsSuspendus { get; set; }
    public int TotalUtilisateurs { get; set; }
}

public record DemandeAccesResponseDto(
    Guid Id,
    string NomEntreprise,
    string NomContact,
    string PrenomContact,
    string Email,
    string Telephone,
    Guid PaysId,
    string Message,
    int Statut, // 0 = EnAttente, 1 = Approuve, 2 = Rejete
    DateTime DateDemande
);

public record ValiderDemandeAccesRequest(
    Guid DemandeId,
    bool Approuver
);
