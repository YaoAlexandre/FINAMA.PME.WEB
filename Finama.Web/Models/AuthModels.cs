namespace Finama.Web.Models;

public class LoginModel
{
    public string Email { get; set; } = string.Empty;
    public string MotDePasse { get; set; } = string.Empty;
}

public class RegisterModel
{
    public string NomEntreprise { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MotDePasse { get; set; } = string.Empty;
    public string NomAdministrateur { get; set; } = string.Empty;
    public string PrenomAdministrateur { get; set; } = string.Empty;
    public Guid PaysId { get; set; } = Guid.Empty;
}

public class PaysDto
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string CodeISO { get; set; } = string.Empty;
}
