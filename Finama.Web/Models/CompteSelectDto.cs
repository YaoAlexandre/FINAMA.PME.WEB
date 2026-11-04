namespace Finama.Web.Models
{
    public class CompteSelectDto
    {
        public Guid Id { get; set; }

        /// <summary>Numéro du compte tel qu'envoyé par l'API (« numero »).</summary>
        public string Numero { get; set; } = string.Empty;

        /// <summary>Numéro affiché et recherché par le sélecteur : « code » s'il est fourni, sinon « numero ».</summary>
        public string Code
        {
            get => string.IsNullOrEmpty(_code) ? Numero : _code;
            set => _code = value ?? string.Empty;
        }
        private string _code = string.Empty;

        public string Libelle { get; set; } = string.Empty;
    }
}