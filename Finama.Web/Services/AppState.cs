using Finama.Web.Models;

namespace Finama.Web.Services
{
    public class AppState
    {
        // Stockez ici ce que vous voulez partager
        public TableauBordDto? DonneesTableauBord { get; set; }
        public AuthResponse Utilisateur { get; set; } 
        public string Devise { get; set; } = "FCFA";

        // Optionnel : Un événement pour notifier les pages quand les données changent
        public event Action? OnChange;
        public void NotifyStateChanged() => OnChange?.Invoke();
    }
}
