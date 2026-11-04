using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Finama.Web.Shared;
using Finama.Web.Features.Commerce.Components;

namespace Finama.Web.Features.Commerce.Pages;

/// <summary>Caisse — utilitaires d'affichage et types internes (panier, reçu).</summary>
public partial class Caisse
{
    // ── Utilitaires ──
    private static (string Classe, string Libelle) EtatStock(ProduitCommerceDto a)
    {
        var q = a.StockActuel.ToString("0.###");
        if (a.StockActuel <= 0) return ("stock-rupture", "Rupture");
        if (a.StockAlerte > 0 && a.StockActuel <= a.StockAlerte) return ("stock-bas", $"Stock bas : {q}");
        return ("stock-ok", $"Stock : {q}");
    }

    private static string UniteSuffixe(ProduitCommerceDto a) => (a.Unite ?? 0) switch
    {
        1 => "/kg",
        2 => "/g",
        3 => "/L",
        4 => "/m",
        5 => "/h",
        _ => ""
    };

    private static string LibelleMode(ModePaiementPos m) => m switch
    {
        ModePaiementPos.Especes => "Espèces",
        ModePaiementPos.Carte => "Carte",
        ModePaiementPos.MobileMoney => "Mobile Money",
        ModePaiementPos.Credit => "Crédit client",
        ModePaiementPos.Points => "Points",
        _ => m.ToString()
    };

    public class PaiementSaisi
    {
        public ModePaiementPos Mode { get; set; }
        public decimal Montant { get; set; }
        public string? Reference { get; set; }
    }

    public class LignePanierItem
    {
        public Guid ProduitId { get; set; }
        public string Designation { get; set; } = "";
        public decimal PrixUnitaire { get; set; }
        public decimal Quantite { get; set; }
        public decimal StockMax { get; set; }
        public bool Vrac { get; set; }
        public string Unite { get; set; } = "";
        public decimal Total => Math.Round(PrixUnitaire * Quantite, 2, MidpointRounding.AwayFromZero);
    }

}
