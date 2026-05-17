namespace Finama.Web.Models
{
    public class CompteSelectDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Libelle { get; set; } = string.Empty;
    }
}
