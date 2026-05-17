namespace Finama.Web.Models;

public class UtilisateurModel
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool EstActif { get; set; }
}

public record InviterUtilisateurRequest(string Nom, string Prenom, string Email, string Role);