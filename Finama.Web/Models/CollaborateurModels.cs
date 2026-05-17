namespace Finama.Web.Models
{
    // DTOs locaux ou à déplacer dans ton fichier global
    public class CreerCollaborateurRequest
    {
        public string Nom { get; set; } = string.Empty;
        public string Prénom { get; set; } = string.Empty; // S'aligne sur l'écriture C#
        public string Prenom { get => Prénom; set => Prénom = value; }
        public string Email { get; set; } = string.Empty;
        public string MotDePasse { get; set; } = string.Empty;
        public int Role { get; set; } = 1; // 1 = Comptable par défaut
    }

    public class UtilisateurDto
    {
        public Guid Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string Prenom { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool EstActif { get; set; }
    }
}
