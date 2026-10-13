using System.ComponentModel.DataAnnotations;

namespace Finama.Web.Models;

public class ChangerMotDePasseModel
{
    [Required(ErrorMessage = "Le mot de passe actuel ou temporaire est requis.")]
    public string AncienMotDePasseOuCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le nouveau mot de passe est requis.")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères.")]
    public string NouveauMotDePasse { get; set; } = string.Empty;

    [Required(ErrorMessage = "La confirmation est requise.")]
    [Compare(nameof(NouveauMotDePasse), ErrorMessage = "Les mots de passe ne correspondent pas.")]
    public string ConfirmerMotDePasse { get; set; } = string.Empty;
}

public class AuthResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;

    public static AuthResult Success(string message = "Opération réussie.")
        => new() { IsSuccess = true, Message = message };

    public static AuthResult Failure(string message)
        => new() { IsSuccess = false, Message = message };
}