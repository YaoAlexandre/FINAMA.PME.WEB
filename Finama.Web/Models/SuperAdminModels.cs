namespace Finama.Web.Models;

public class TenantAdminDto
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Pays { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
    public DateTime? AbonnementExpireAt { get; set; }
    public bool EstActif { get; set; }
    public int NbUtilisateurs { get; set; }

    public int? JoursRestants => AbonnementExpireAt.HasValue
        ? (int)Math.Ceiling((AbonnementExpireAt.Value - DateTime.UtcNow).TotalDays)
        : null;
}

public class UtilisateurAdminDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool EstActif { get; set; }
    public DateTime? DerniereConnexionAt { get; set; }
}

// Le nom de la propriété doit être EstActif : c'est ce que l'API attend.
public class ChangerStatutTenantRequest
{
    public bool EstActif { get; set; }
}

public class ActiverAbonnementRequest
{
    public int Plan { get; set; }
    public int DureeJours { get; set; }
}

public record ResultatApi(bool Succes, string Message);

public record PlanOption(int Valeur, string Libelle);

public static class PlansAbonnement
{
    // À ALIGNER sur les valeurs réelles de l'enum PlanAbonnement côté API.
    public static readonly List<PlanOption> Liste = new()
    {
        new(1, "Starter"),
        new(2, "Pro"),
        new(3, "Business")
    };
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
